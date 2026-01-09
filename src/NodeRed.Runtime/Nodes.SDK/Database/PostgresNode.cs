// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using Npgsql;
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
- **Query**: SELECT statements, returns array of rows as dictionaries
- **Execute**: INSERT/UPDATE/DELETE, returns affected count  
- **Function**: Calls PostgreSQL functions

**Parameters:**
PostgreSQL uses positional parameters ($1, $2, etc.). Pass an array in msg.payload.

**Example:**
```
msg.payload = [123, 'active'];
msg.query = 'SELECT * FROM users WHERE id = $1 AND status = $2';
```")
        .Build();

    protected override async Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var host = GetConfig("host", "localhost");
            var port = GetConfig("port", 5432);
            var database = GetConfig<string>("database", "");
            var username = GetConfig<string>("username", "");
            var password = GetConfig<string>("password", "");
            var schema = GetConfig("schema", "public");
            var useSsl = GetConfig("useSsl", false);
            var timeout = GetConfig("timeout", 30);
            var operation = GetConfig("operation", "query");
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
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Database = database,
                Username = username,
                Password = password,
                Timeout = timeout,
                SslMode = useSsl ? SslMode.Require : SslMode.Prefer,
                SearchPath = schema
            };

            Status($"Connecting to {database}...", StatusFill.Yellow, SdkStatusShape.Ring);

            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(query, connection);
            command.CommandTimeout = timeout;

            // Add positional parameters from msg.payload if it's an array
            if (msg.Payload is IList<object?> parameters)
            {
                for (var i = 0; i < parameters.Count; i++)
                {
                    command.Parameters.AddWithValue(parameters[i] ?? DBNull.Value);
                }
            }
            else if (msg.Payload is object[] paramArray)
            {
                foreach (var param in paramArray)
                {
                    command.Parameters.AddWithValue(param ?? DBNull.Value);
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
            NpgsqlTypes.NpgsqlPoint p => new { x = p.X, y = p.Y },
            _ => value
        };
    }
}
