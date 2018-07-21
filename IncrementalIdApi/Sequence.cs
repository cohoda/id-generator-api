using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace IncrementalIdApi
{
    public class DirectGetter
    {
        private static string dbName;
        private static string collectionName;
        private static DocumentClient client;

        private static async Task<CosmosResult> CallDirectly(string vertical)
        {
            double reqCharge = 0;

            var id = "Seq_" + vertical.ToLower();

            DocumentResponse<Sequence> existingSequenceDocument = null;

            try
            {
                existingSequenceDocument = await client.ReadDocumentAsync<Sequence>(UriFactory.CreateDocumentUri(dbName, collectionName, id));
                reqCharge = reqCharge + existingSequenceDocument.RequestCharge;
            }
            catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                //do nothing
            }

            if (existingSequenceDocument == null)
            {
                var newSequence = new Sequence { Vertical = vertical, Value = 1 };
                var result = await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(dbName, collectionName), newSequence);

                if (result.StatusCode == HttpStatusCode.Created)
                    return new CosmosResult(1, reqCharge);
            }

            //increment an existing sequence
            var currentSequenceValue = existingSequenceDocument.Document.Value;
            var incrementedSequence = new Sequence() { Vertical = vertical, Value = currentSequenceValue + 1 };

            var res = await client.ReplaceDocumentAsync(UriFactory.CreateDocumentUri(dbName, collectionName, existingSequenceDocument.Document.id), incrementedSequence, new RequestOptions
            {
                AccessCondition = new AccessCondition { Condition = existingSequenceDocument.ResponseHeaders["ETag"], Type = AccessConditionType.IfMatch }
            });

            reqCharge = reqCharge + res.RequestCharge;

            return new CosmosResult(incrementedSequence.Value, reqCharge);
        }

    }

    public class CosmosResult
    {
        public CosmosResult(int value, double requestCharge)
        {
            Value = value;
            RequestCharge = requestCharge;
        }

        public int Value { get; }
        public double RequestCharge { get; }
    }

    public class Sequence
    {
        //used by cosmos
        public string id => Type + "_" + Vertical.ToLower();

        public string Type => "Seq";

        public string Vertical { get; set; }

        public int Value { get; set; }
    }
}
