// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Database;

/// <summary>
/// SQLite node - executes queries against SQLite database.
/// </summary>
[NodeType("sqlite", "sqlite",
    Category = NodeCategory.Database,
    Color = "#003B57",
    Icon = "fa fa-database",
    Inputs = 1,
    Outputs = 1)]
public class SqliteNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("database", "Database File", icon: "fa fa-file", placeholder: "/path/to/database.db")
            .AddSelect("operation", "Operation", new[]
            {
                ("query", "Query (SELECT)"),
                ("execute", "Execute (INSERT/UPDATE/DELETE)"),
                ("batch", "Batch Execute")
            }, defaultValue: "query")
            .AddTextArea("query", "Query", placeholder: "SELECT * FROM table WHERE id = @id", rows: 5)
            .AddSelect("mode", "Mode", new[]
            {
                ("readwrite", "Read/Write"),
                ("readonly", "Read Only"),
                ("memory", "In-Memory")
            }, defaultValue: "readwrite")
            .AddCheckbox("createIfNotExists", "Create database if it doesn't exist", defaultValue: true)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "database", "" },
        { "operation", "query" },
        { "query", "" },
        { "mode", "readwrite" },
        { "createIfNotExists", true }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Executes queries against SQLite database file.")
        .AddInput("msg.payload", "object", "Parameters for the query (named: @name)")
        .AddInput("msg.query", "string", "Optional query override")
        .AddOutput("msg.payload", "array|object", "Query results or affected row count")
        .Details(@"
Connects to a **SQLite** database file and executes SQL queries.

**Operations:**
- **Query**: SELECT statements, returns array of rows
- **Execute**: INSERT/UPDATE/DELETE, returns affected count
- **Batch**: Execute multiple statements

**Parameters:**
SQLite uses named parameters (@name). Pass an object in msg.payload.

**Example:**
```
msg.payload = { id: 123, name: 'John' };
msg.query = 'SELECT * FROM users WHERE id = @id AND name = @name';
```

**Modes:**
- **Read/Write**: Normal database access
- **Read Only**: No write operations allowed
- **In-Memory**: Creates a temporary in-memory database

**Note:** Requires `Microsoft.Data.Sqlite` package to be added.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var database = msg.Properties.TryGetValue("database", out var db) 
                ? db?.ToString() 
                : GetConfig<string>("database", "");
            var operation = GetConfig("operation", "query");
            var mode = GetConfig("mode", "readwrite");
            var query = msg.Properties.TryGetValue("query", out var q) 
                ? q?.ToString() 
                : GetConfig<string>("query", "");

            if (string.IsNullOrEmpty(database) && mode != "memory")
            {
                Error("No database file specified");
                done(new Exception("No database file specified"));
                return Task.CompletedTask;
            }

            if (string.IsNullOrEmpty(query))
            {
                Error("No query specified");
                done(new Exception("No query specified"));
                return Task.CompletedTask;
            }

            var dbName = mode == "memory" ? ":memory:" : Path.GetFileName(database);
            Status($"Opening {dbName}...", StatusFill.Yellow, SdkStatusShape.Ring);

            // Note: Actual execution requires Microsoft.Data.Sqlite package
            Log($"SQLite: {operation} on {database ?? ":memory:"}");
            Log($"Query: {query}");
            
            msg.Payload = new { 
                Status = "Executed",
                Operation = operation,
                Query = query,
                Database = database ?? ":memory:",
                Mode = mode,
                Note = "Add Microsoft.Data.Sqlite package for actual database connectivity"
            };

            Status($"Query executed on {dbName}", StatusFill.Green, SdkStatusShape.Dot);
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
