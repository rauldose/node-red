// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Database;

/// <summary>
/// MongoDB node - executes operations against MongoDB database.
/// </summary>
[NodeType("mongodb", "mongodb",
    Category = NodeCategory.Database,
    Color = "#4DB33D",
    Icon = "fa fa-leaf",
    Inputs = 1,
    Outputs = 1)]
public class MongoDbNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("uri", "Connection URI", icon: "fa fa-link", 
                placeholder: "mongodb://localhost:27017")
            .AddText("database", "Database", icon: "fa fa-database")
            .AddText("collection", "Collection", icon: "fa fa-table")
            .AddSelect("operation", "Operation", new[]
            {
                ("find", "Find"),
                ("findOne", "Find One"),
                ("insertOne", "Insert One"),
                ("insertMany", "Insert Many"),
                ("updateOne", "Update One"),
                ("updateMany", "Update Many"),
                ("deleteOne", "Delete One"),
                ("deleteMany", "Delete Many"),
                ("aggregate", "Aggregate"),
                ("count", "Count")
            }, defaultValue: "find")
            .AddTextArea("query", "Query/Filter", placeholder: "{ \"status\": \"active\" }", rows: 4)
            .AddTextArea("update", "Update/Document", placeholder: "{ \"$set\": { \"status\": \"inactive\" } }", rows: 4)
            .AddNumber("limit", "Limit", defaultValue: 0, suffix: "0 = no limit")
            .AddNumber("skip", "Skip", defaultValue: 0)
            .AddText("sort", "Sort", placeholder: "{ \"createdAt\": -1 }")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "uri", "mongodb://localhost:27017" },
        { "database", "" },
        { "collection", "" },
        { "operation", "find" },
        { "query", "{}" },
        { "update", "" },
        { "limit", 0 },
        { "skip", 0 },
        { "sort", "" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Executes operations against MongoDB database.")
        .AddInput("msg.payload", "object", "Document or filter for the operation")
        .AddInput("msg.collection", "string", "Optional collection override")
        .AddOutput("msg.payload", "array|object", "Query results or operation result")
        .Details(@"
Connects to **MongoDB** and executes database operations.

**Operations:**
- **Find**: Query documents, returns array
- **Find One**: Get single document
- **Insert One/Many**: Add documents
- **Update One/Many**: Modify documents
- **Delete One/Many**: Remove documents
- **Aggregate**: Run aggregation pipeline
- **Count**: Count matching documents

**Query Format:**
Use JSON format for queries and updates:
```json
{ ""status"": ""active"", ""age"": { ""$gt"": 21 } }
```

**Update Format:**
```json
{ ""$set"": { ""status"": ""inactive"" } }
```")
        .Build();

    protected override async Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var uri = GetConfig("uri", "mongodb://localhost:27017");
            var databaseName = GetConfig<string>("database", "");
            var collectionName = msg.Properties.TryGetValue("collection", out var c) 
                ? c?.ToString() 
                : GetConfig<string>("collection", "");
            var operation = GetConfig("operation", "find");
            var queryJson = GetConfig<string>("query", "{}");
            var updateJson = GetConfig<string>("update", "");
            var limit = GetConfig("limit", 0);
            var skip = GetConfig("skip", 0);
            var sortJson = GetConfig<string>("sort", "");

            if (string.IsNullOrEmpty(databaseName))
            {
                Error("No database specified");
                done(new Exception("No database specified"));
                return;
            }

            if (string.IsNullOrEmpty(collectionName))
            {
                Error("No collection specified");
                done(new Exception("No collection specified"));
                return;
            }

            Status($"Connecting to {databaseName}...", StatusFill.Yellow, SdkStatusShape.Ring);

            var client = new MongoClient(uri);
            var database = client.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);

            // Parse filter from query or msg.payload
            BsonDocument filter;
            var payloadDict = msg.Payload as IDictionary<string, object?>;
            var usePayloadAsFilter = payloadDict != null && (operation.StartsWith("find") || operation == "count" || operation.StartsWith("delete") || operation.StartsWith("update"));
            
            if (usePayloadAsFilter && payloadDict != null)
            {
                filter = BsonDocument.Parse(JsonSerializer.Serialize(payloadDict));
            }
            else
            {
                filter = BsonDocument.Parse(queryJson ?? "{}");
            }

            switch (operation)
            {
                case "find":
                    var findOptions = new FindOptions<BsonDocument>();
                    if (limit > 0) findOptions.Limit = limit;
                    if (skip > 0) findOptions.Skip = skip;
                    if (!string.IsNullOrEmpty(sortJson))
                    {
                        findOptions.Sort = BsonDocument.Parse(sortJson);
                    }
                    
                    var cursor = await collection.FindAsync(filter, findOptions);
                    var documents = await cursor.ToListAsync();
                    msg.Payload = documents.Select(BsonDocumentToDictionary).ToList();
                    Status($"{documents.Count} documents found", StatusFill.Green, SdkStatusShape.Dot);
                    break;

                case "findOne":
                    var doc = await collection.Find(filter).FirstOrDefaultAsync();
                    msg.Payload = doc != null ? BsonDocumentToDictionary(doc) : null;
                    Status(doc != null ? "Document found" : "No document found", StatusFill.Green, SdkStatusShape.Dot);
                    break;

                case "insertOne":
                    var insertDoc = msg.Payload is IDictionary<string, object?> idict 
                        ? BsonDocument.Parse(JsonSerializer.Serialize(idict))
                        : BsonDocument.Parse(updateJson ?? "{}");
                    await collection.InsertOneAsync(insertDoc);
                    msg.Payload = new Dictionary<string, object?> { { "insertedId", insertDoc["_id"].ToString() } };
                    Status("Document inserted", StatusFill.Green, SdkStatusShape.Dot);
                    break;

                case "insertMany":
                    var insertDocs = msg.Payload is IList<object?> list 
                        ? list.Select(d => BsonDocument.Parse(JsonSerializer.Serialize(d))).ToList()
                        : new List<BsonDocument>();
                    if (insertDocs.Count > 0)
                    {
                        await collection.InsertManyAsync(insertDocs);
                        msg.Payload = new Dictionary<string, object?> { { "insertedCount", insertDocs.Count } };
                        Status($"{insertDocs.Count} documents inserted", StatusFill.Green, SdkStatusShape.Dot);
                    }
                    break;

                case "updateOne":
                    var updateOne = BsonDocument.Parse(updateJson ?? "{}");
                    var updateOneResult = await collection.UpdateOneAsync(filter, updateOne);
                    msg.Payload = new Dictionary<string, object?> 
                    { 
                        { "matchedCount", updateOneResult.MatchedCount },
                        { "modifiedCount", updateOneResult.ModifiedCount }
                    };
                    Status($"{updateOneResult.ModifiedCount} document(s) updated", StatusFill.Green, SdkStatusShape.Dot);
                    break;

                case "updateMany":
                    var updateMany = BsonDocument.Parse(updateJson ?? "{}");
                    var updateManyResult = await collection.UpdateManyAsync(filter, updateMany);
                    msg.Payload = new Dictionary<string, object?> 
                    { 
                        { "matchedCount", updateManyResult.MatchedCount },
                        { "modifiedCount", updateManyResult.ModifiedCount }
                    };
                    Status($"{updateManyResult.ModifiedCount} document(s) updated", StatusFill.Green, SdkStatusShape.Dot);
                    break;

                case "deleteOne":
                    var deleteOneResult = await collection.DeleteOneAsync(filter);
                    msg.Payload = new Dictionary<string, object?> { { "deletedCount", deleteOneResult.DeletedCount } };
                    Status($"{deleteOneResult.DeletedCount} document(s) deleted", StatusFill.Green, SdkStatusShape.Dot);
                    break;

                case "deleteMany":
                    var deleteManyResult = await collection.DeleteManyAsync(filter);
                    msg.Payload = new Dictionary<string, object?> { { "deletedCount", deleteManyResult.DeletedCount } };
                    Status($"{deleteManyResult.DeletedCount} document(s) deleted", StatusFill.Green, SdkStatusShape.Dot);
                    break;

                case "aggregate":
                    var pipeline = BsonSerializer.Deserialize<BsonDocument[]>(queryJson ?? "[]");
                    var aggCursor = await collection.AggregateAsync<BsonDocument>(pipeline);
                    var aggResults = await aggCursor.ToListAsync();
                    msg.Payload = aggResults.Select(BsonDocumentToDictionary).ToList();
                    Status($"{aggResults.Count} documents returned", StatusFill.Green, SdkStatusShape.Dot);
                    break;

                case "count":
                    var count = await collection.CountDocumentsAsync(filter);
                    msg.Payload = new Dictionary<string, object?> { { "count", count } };
                    Status($"Count: {count}", StatusFill.Green, SdkStatusShape.Dot);
                    break;
            }

            send(0, msg);
            done();
        }
        catch (Exception ex)
        {
            Error(ex.Message);
            Status("Error", StatusFill.Red, SdkStatusShape.Ring);
            done(ex);
        }
    }

    private static Dictionary<string, object?> BsonDocumentToDictionary(BsonDocument doc)
    {
        var result = new Dictionary<string, object?>();
        foreach (var element in doc)
        {
            result[element.Name] = BsonValueToObject(element.Value);
        }
        return result;
    }

    private static object? BsonValueToObject(BsonValue value)
    {
        return value.BsonType switch
        {
            BsonType.Null => null,
            BsonType.ObjectId => value.AsObjectId.ToString(),
            BsonType.String => value.AsString,
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Double => value.AsDouble,
            BsonType.Boolean => value.AsBoolean,
            BsonType.DateTime => value.ToUniversalTime().ToString("O"),
            BsonType.Array => value.AsBsonArray.Select(BsonValueToObject).ToList(),
            BsonType.Document => BsonDocumentToDictionary(value.AsBsonDocument),
            BsonType.Binary => Convert.ToBase64String(value.AsByteArray),
            _ => value.ToString()
        };
    }
}
