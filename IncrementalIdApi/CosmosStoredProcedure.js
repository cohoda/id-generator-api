function Go(sequenceId) {
    const context = getContext();
    const $ = context.getCollection();

    const incrementSequence = function (error, docs) {
        if (error) throw new Error(`Increment error ${error.message}`);

        var sequenceDocument = null;

        if (typeof docs === "undefined" || !docs || !docs.length) {
            sequenceDocument = {
                id: `Seq_${sequenceId}`,
                value: 0
            };
        } else {
            sequenceDocument = docs[0];
        }

        ++sequenceDocument.value;

        $.upsertDocument($.getSelfLink(), sequenceDocument, {}, function (upsertErr) {
            if (upsertErr) throw new Error(`Save error ${upsertErr.code}: ${error.body}`);
            context.getResponse().setBody(sequenceDocument.value);
        });
    };

    $.queryDocuments($.getSelfLink(),
        `SELECT * FROM s WHERE s.id  = "Seq_${sequenceId}"`,
        {}, incrementSequence);
}