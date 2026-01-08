// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

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
```

**Note:** Requires `MongoDB.Driver` package to be added.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var uri = GetConfig("uri", "mongodb://localhost:27017");
            var database = GetConfig<string>("database", "");
            var collection = msg.Properties.TryGetValue("collection", out var c) 
                ? c?.ToString() 
                : GetConfig<string>("collection", "");
            var operation = GetConfig("operation", "find");
            var query = GetConfig<string>("query", "{}");
            var limit = GetConfig("limit", 0);

            if (string.IsNullOrEmpty(database))
            {
                Error("No database specified");
                done(new Exception("No database specified"));
                return Task.CompletedTask;
            }

            if (string.IsNullOrEmpty(collection))
            {
                Error("No collection specified");
                done(new Exception("No collection specified"));
                return Task.CompletedTask;
            }

            Status($"Connecting to {database}...", StatusFill.Yellow, SdkStatusShape.Ring);

            // Note: Actual execution requires MongoDB.Driver package
            Log($"MongoDB: {operation} on {database}.{collection}");
            Log($"Query: {query}");
            
            msg.Payload = new { 
                Status = "Executed",
                Operation = operation,
                Query = query,
                Database = database,
                Collection = collection,
                Limit = limit,
                Note = "Add MongoDB.Driver package for actual database connectivity"
            };

            Status($"{operation} on {collection}", StatusFill.Green, SdkStatusShape.Dot);
            send(0, msg);
            done();
        }
        catch (Exception ex)
        {
            Error(ex.Message);
            Status("Error", StatusFill.Red, SdkStatusShape.Ring);
            done(ex);
        }

        return Task.CompletedTask;
    }
}
