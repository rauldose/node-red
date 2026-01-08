// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Data;
using MySqlConnector;
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
- **Query**: SELECT statements, returns array of rows as dictionaries
- **Execute**: INSERT/UPDATE/DELETE, returns affected count
- **Procedure**: Calls stored procedures

**Parameters:**
MySQL uses `?` for positional parameters. Pass an array in msg.payload.

**Example:**
```
msg.payload = [123, 'John'];
msg.query = 'SELECT * FROM users WHERE id = ? AND name = ?';
```")
        .Build();

    protected override async Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var host = GetConfig("host", "localhost");
            var port = GetConfig("port", 3306);
            var database = GetConfig<string>("database", "");
            var username = GetConfig<string>("username", "");
            var password = GetConfig<string>("password", "");
            var useSsl = GetConfig("useSsl", false);
            var timeout = GetConfig("timeout", 30);
            var charset = GetConfig("charset", "utf8mb4");
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
            var builder = new MySqlConnectionStringBuilder
            {
                Server = host,
                Port = (uint)port,
                Database = database,
                UserID = username,
                Password = password,
                ConnectionTimeout = (uint)timeout,
                CharacterSet = charset,
                SslMode = useSsl ? MySqlSslMode.Required : MySqlSslMode.Preferred
            };

            Status($"Connecting to {database}...", StatusFill.Yellow, SdkStatusShape.Ring);

            await using var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(query, connection);
            command.CommandTimeout = timeout;

            // Handle stored procedure
            if (operation == "procedure")
            {
                command.CommandType = CommandType.StoredProcedure;
            }

            // Add positional parameters from msg.payload if it's an array
            if (msg.Payload is IList<object?> parameters)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue("", param ?? DBNull.Value);
                }
            }
            else if (msg.Payload is object[] paramArray)
            {
                foreach (var param in paramArray)
                {
                    command.Parameters.AddWithValue("", param ?? DBNull.Value);
                }
            }

            if (operation == "execute")
            {
                var affectedRows = await command.ExecuteNonQueryAsync();
                msg.Payload = new Dictionary<string, object?> 
                { 
                    { "affectedRows", affectedRows },
                    { "lastInsertId", command.LastInsertedId }
                };
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
            TimeSpan ts => ts.ToString(),
            Guid g => g.ToString(),
            decimal d => (double)d,
            _ => value
        };
    }
}
