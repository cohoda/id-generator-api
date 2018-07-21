using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace IncrementalIdApi
{
    public static class GetIncrementIdFunction
    {
        private static readonly string EndpointUrl = Env("COSMOS_ENDPOINT_URL");
        private static readonly string AuthorizationKey = Env("COSMOS_AUTH_KEY");
        private static readonly string DatabaseId = Env("COSMOS_DATABASE_ID");
        private static readonly string CollectionId = Env("COSMOS_COLLECTION_ID");
        private const string StoredProcId = "IdStore.IncrementId";

        private static readonly DocumentClient CosmosDbClient = new DocumentClient(new Uri(EndpointUrl), AuthorizationKey);

        static GetIncrementIdFunction()
        {
            InitialiseStoredProcedure()
                .Wait();
        }

        [FunctionName("GetIncrementalId")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            var vertical = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "vertical", StringComparison.OrdinalIgnoreCase) == 0)
                .Value;

            if (string.IsNullOrEmpty(vertical))
                return req.CreateResponse(HttpStatusCode.BadRequest, "Vertical must be supplied");


            var storedProcedureResponse = await CallStoredProcedure(vertical);

            return req.CreateResponse(HttpStatusCode.OK, storedProcedureResponse.Response);
        }

        private static async Task<StoredProcedureResponse<int>> CallStoredProcedure(string vertical)
        {
            try
            {
                return await CosmosDbClient.ExecuteStoredProcedureAsync<int>(UriFactory.CreateStoredProcedureUri(DatabaseId, CollectionId, StoredProcId), vertical);
            }
            catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                //if the stored procedure isn't found, it must have been deleted, without the function re-initialising
                await InitialiseStoredProcedure();

                return await CallStoredProcedure(vertical);
            }
        }

        private static async Task InitialiseStoredProcedure()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var rootNameSpace = assembly.GetTypes().Select(t => t.Namespace).Distinct().First();
            using (var stream = assembly.GetManifestResourceStream(rootNameSpace+".CosmosStoredProcedure.js"))
            using (var reader = new StreamReader(stream))
            {
                await CosmosDbClient.UpsertStoredProcedureAsync(
                    UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId), new StoredProcedure
                    {
                        Id = StoredProcId,
                        Body = reader.ReadToEnd()
                    });
            }
        }

        public static string Env(string name) => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
    }
}
