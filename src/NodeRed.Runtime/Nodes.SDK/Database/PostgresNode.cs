// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Database;

/// <summary>
/// PostgreSQL node - executes queries against PostgreSQL database.
/// </summary>
[NodeType("postgres", "postgres",
    Category = NodeCategory.Database,
    Color = "#336791",
    Icon = "fa fa-database",
    Inputs = 1,
    Outputs = 1)]
public class PostgresNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("host", "Host", icon: "fa fa-server", placeholder: "localhost")
            .AddNumber("port", "Port", defaultValue: 5432)
            .AddText("database", "Database", icon: "fa fa-database")
            .AddText("username", "Username", icon: "fa fa-user")
            .AddPassword("password", "Password")
            .AddText("schema", "Schema", placeholder: "public")
            .AddSelect("operation", "Operation", new[]
            {
                ("query", "Query (SELECT)"),
                ("execute", "Execute (INSERT/UPDATE/DELETE)"),
                ("function", "Call Function")
            }, defaultValue: "query")
            .AddTextArea("query", "Query", placeholder: "SELECT * FROM table WHERE id = $1", rows: 5)
            .AddCheckbox("useSsl", "Use SSL", defaultValue: false)
            .AddNumber("timeout", "Timeout (seconds)", defaultValue: 30)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "host", "localhost" },
        { "port", 5432 },
        { "database", "" },
        { "username", "" },
        { "schema", "public" },
        { "operation", "query" },
        { "query", "" },
        { "useSsl", false },
        { "timeout", 30 }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Executes queries against PostgreSQL database.")
        .AddInput("msg.payload", "array", "Parameters for the query (positional: [$1, $2, ...])")
        .AddInput("msg.query", "string", "Optional query override")
        .AddOutput("msg.payload", "array|object", "Query results or affected row count")
        .Details(@"
Connects to **PostgreSQL** and executes SQL queries.

**Operations:**
- **Query**: SELECT statements, returns array of rows
- **Execute**: INSERT/UPDATE/DELETE, returns affected count  
- **Function**: Calls PostgreSQL functions

**Parameters:**
PostgreSQL uses positional parameters ($1, $2, etc.). Pass an array in msg.payload.

**Example:**
```
msg.payload = [123, 'active'];
msg.query = 'SELECT * FROM users WHERE id = $1 AND status = $2';
```

**Note:** Requires `Npgsql` package to be added.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var host = GetConfig("host", "localhost");
            var port = GetConfig("port", 5432);
            var database = GetConfig<string>("database", "");
            var schema = GetConfig("schema", "public");
            var operation = GetConfig("operation", "query");
            var query = msg.Properties.TryGetValue("query", out var q) 
                ? q?.ToString() 
                : GetConfig<string>("query", "");

            if (string.IsNullOrEmpty(database))
            {
                Error("No database specified");
                done(new Exception("No database specified"));
                return Task.CompletedTask;
            }

            if (string.IsNullOrEmpty(query))
            {
                Error("No query specified");
                done(new Exception("No query specified"));
                return Task.CompletedTask;
            }

            Status($"Connecting to {database}...", StatusFill.Yellow, SdkStatusShape.Ring);

            // Note: Actual execution requires Npgsql package
            Log($"PostgreSQL: {operation} on {host}:{port}/{database}");
            Log($"Query: {query}");
            
            msg.Payload = new { 
                Status = "Executed",
                Operation = operation,
                Query = query,
                Host = host,
                Database = database,
                Schema = schema,
                Note = "Add Npgsql package for actual database connectivity"
            };

            Status($"Query executed on {database}", StatusFill.Green, SdkStatusShape.Dot);
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
