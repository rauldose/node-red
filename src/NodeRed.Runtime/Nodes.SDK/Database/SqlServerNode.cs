// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Data;
using Microsoft.Data.SqlClient;
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
- **Query**: SELECT statements, returns array of rows as dictionaries
- **Execute**: INSERT/UPDATE/DELETE, returns affected count
- **Stored Procedure**: Calls stored procedures

**Parameters:**
Parameters from msg.payload are passed to the query using `@paramName` syntax.

**Result Types:**
- Query results are returned as `List<Dictionary<string, object?>>` with proper type casting
- Execute returns `{ affectedRows: int }`")
        .Build();

    protected override async Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var server = GetConfig("server", "localhost");
            var port = GetConfig("port", 1433);
            var database = GetConfig<string>("database", "");
            var username = GetConfig<string>("username", "");
            var password = GetConfig<string>("password", "");
            var operation = GetConfig("operation", "query");
            var usePool = GetConfig("usePool", true);
            var timeout = GetConfig("timeout", 30);
            var query = msg.Properties.TryGetValue("query", out var q) 
                ? q?.ToString() 
                : GetConfig<string>("query", "");

            if (string.IsNullOrEmpty(database))
            {
                Error("No database specified");
                done(new Exception("No database specified"));
                return;
            }

            if (string.IsNullOrEmpty(query))
            {
                Error("No query specified");
                done(new Exception("No query specified"));
                return;
            }

            // Build connection string
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = $"{server},{port}",
                InitialCatalog = database,
                Pooling = usePool,
                ConnectTimeout = timeout
            };

            if (!string.IsNullOrEmpty(username))
            {
                builder.UserID = username;
                builder.Password = password;
            }
            else
            {
                builder.IntegratedSecurity = true;
                builder.TrustServerCertificate = true;
            }

            Status($"Connecting to {database}...", StatusFill.Yellow, SdkStatusShape.Ring);

            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(query, connection);
            command.CommandTimeout = timeout;

            // Handle stored procedure
            if (operation == "storedproc")
            {
                command.CommandType = CommandType.StoredProcedure;
            }

            // Add parameters from msg.payload if it's a dictionary
            if (msg.Payload is IDictionary<string, object?> parameters)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue($"@{param.Key}", param.Value ?? DBNull.Value);
                }
            }

            if (operation == "execute")
            {
                var affectedRows = await command.ExecuteNonQueryAsync();
                msg.Payload = new Dictionary<string, object?> { { "affectedRows", affectedRows } };
                Status($"{affectedRows} rows affected", StatusFill.Green, SdkStatusShape.Dot);
            }
            else
            {
                await using var reader = await command.ExecuteReaderAsync();
                var results = new List<Dictionary<string, object?>>();

                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.IsDBNull(i) ? null : CastValue(reader.GetValue(i));
                        row[reader.GetName(i)] = value;
                    }
                    results.Add(row);
                }

                msg.Payload = results;
                Status($"{results.Count} rows returned", StatusFill.Green, SdkStatusShape.Dot);
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

    private static object? CastValue(object value)
    {
        return value switch
        {
            DBNull => null,
            byte[] bytes => Convert.ToBase64String(bytes),
            DateTime dt => dt.ToString("O"),
            DateTimeOffset dto => dto.ToString("O"),
            TimeSpan ts => ts.ToString(),
            Guid g => g.ToString(),
            decimal d => (double)d,
            _ => value
        };
    }
}
