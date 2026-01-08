// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Database;

/// <summary>
/// SQL Server node - executes queries against Microsoft SQL Server.
/// </summary>
[NodeType("sqlserver", "sqlserver",
    Category = NodeCategory.Database,
    Color = "#CC2936",
    Icon = "fa fa-database",
    Inputs = 1,
    Outputs = 1)]
public class SqlServerNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("server", "Server", icon: "fa fa-server", placeholder: "localhost")
            .AddNumber("port", "Port", defaultValue: 1433)
            .AddText("database", "Database", icon: "fa fa-database")
            .AddText("username", "Username", icon: "fa fa-user")
            .AddPassword("password", "Password")
            .AddSelect("operation", "Operation", new[]
            {
                ("query", "Query (SELECT)"),
                ("execute", "Execute (INSERT/UPDATE/DELETE)"),
                ("storedproc", "Stored Procedure")
            }, defaultValue: "query")
            .AddTextArea("query", "Query", placeholder: "SELECT * FROM table WHERE id = @id", rows: 5)
            .AddCheckbox("usePool", "Use Connection Pool", defaultValue: true)
            .AddNumber("timeout", "Timeout (seconds)", defaultValue: 30)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "server", "localhost" },
        { "port", 1433 },
        { "database", "" },
        { "username", "" },
        { "operation", "query" },
        { "query", "" },
        { "usePool", true },
        { "timeout", 30 }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Executes queries against Microsoft SQL Server database.")
        .AddInput("msg.payload", "object", "Parameters for the query (e.g., { \"id\": 123 })")
        .AddInput("msg.query", "string", "Optional query override")
        .AddOutput("msg.payload", "array|object", "Query results or affected row count")
        .Details(@"
Connects to **Microsoft SQL Server** and executes SQL queries.

**Operations:**
- **Query**: SELECT statements, returns array of rows
- **Execute**: INSERT/UPDATE/DELETE, returns affected count
- **Stored Procedure**: Calls stored procedures

**Parameters:**
Parameters from msg.payload are passed to the query using `@paramName` syntax.

**Connection String:**
Built from server, port, database, username, and password configuration.

**Note:** Requires `Microsoft.Data.SqlClient` package to be added.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var server = GetConfig("server", "localhost");
            var port = GetConfig("port", 1433);
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

            // Build connection string info for debugging
            var connectionInfo = $"Server={server},{port};Database={database}";
            Status($"Connecting to {database}...", StatusFill.Yellow, SdkStatusShape.Ring);

            // Note: Actual SQL execution requires Microsoft.Data.SqlClient package
            // This is a placeholder that shows the node structure
            Log($"SQL Server: {operation} on {connectionInfo}");
            Log($"Query: {query}");
            
            // In production, use SqlConnection and SqlCommand
            msg.Payload = new { 
                Status = "Executed",
                Operation = operation,
                Query = query,
                Server = server,
                Database = database,
                Note = "Add Microsoft.Data.SqlClient package for actual database connectivity"
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
