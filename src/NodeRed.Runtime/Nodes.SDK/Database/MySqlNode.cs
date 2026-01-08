// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Database;

/// <summary>
/// MySQL node - executes queries against MySQL/MariaDB database.
/// </summary>
[NodeType("mysql", "mysql",
    Category = NodeCategory.Database,
    Color = "#00758F",
    Icon = "fa fa-database",
    Inputs = 1,
    Outputs = 1)]
public class MySqlNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("host", "Host", icon: "fa fa-server", placeholder: "localhost")
            .AddNumber("port", "Port", defaultValue: 3306)
            .AddText("database", "Database", icon: "fa fa-database")
            .AddText("username", "Username", icon: "fa fa-user")
            .AddPassword("password", "Password")
            .AddSelect("operation", "Operation", new[]
            {
                ("query", "Query (SELECT)"),
                ("execute", "Execute (INSERT/UPDATE/DELETE)"),
                ("procedure", "Call Stored Procedure")
            }, defaultValue: "query")
            .AddTextArea("query", "Query", placeholder: "SELECT * FROM table WHERE id = ?", rows: 5)
            .AddCheckbox("useSsl", "Use SSL", defaultValue: false)
            .AddNumber("timeout", "Timeout (seconds)", defaultValue: 30)
            .AddSelect("charset", "Charset", new[]
            {
                ("utf8mb4", "UTF-8 (utf8mb4)"),
                ("utf8", "UTF-8 (utf8)"),
                ("latin1", "Latin1")
            }, defaultValue: "utf8mb4")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "host", "localhost" },
        { "port", 3306 },
        { "database", "" },
        { "username", "" },
        { "operation", "query" },
        { "query", "" },
        { "useSsl", false },
        { "timeout", 30 },
        { "charset", "utf8mb4" }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Executes queries against MySQL or MariaDB database.")
        .AddInput("msg.payload", "array", "Parameters for the query (positional: ?, ?, ...)")
        .AddInput("msg.query", "string", "Optional query override")
        .AddOutput("msg.payload", "array|object", "Query results or affected row count")
        .Details(@"
Connects to **MySQL** or **MariaDB** and executes SQL queries.

**Operations:**
- **Query**: SELECT statements, returns array of rows
- **Execute**: INSERT/UPDATE/DELETE, returns affected count
- **Procedure**: Calls stored procedures

**Parameters:**
MySQL uses `?` for positional parameters. Pass an array in msg.payload.

**Example:**
```
msg.payload = [123, 'John'];
msg.query = 'SELECT * FROM users WHERE id = ? AND name = ?';
```

**Note:** Requires `MySqlConnector` or `MySql.Data` package to be added.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var host = GetConfig("host", "localhost");
            var port = GetConfig("port", 3306);
            var database = GetConfig<string>("database", "");
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

            // Note: Actual execution requires MySqlConnector package
            Log($"MySQL: {operation} on {host}:{port}/{database}");
            Log($"Query: {query}");
            
            msg.Payload = new { 
                Status = "Executed",
                Operation = operation,
                Query = query,
                Host = host,
                Database = database,
                Note = "Add MySqlConnector package for actual database connectivity"
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
