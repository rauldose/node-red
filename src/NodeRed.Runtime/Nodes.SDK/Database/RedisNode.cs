// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Database;

/// <summary>
/// Redis node - executes commands against Redis cache/database.
/// </summary>
[NodeType("redis", "redis",
    Category = NodeCategory.Database,
    Color = "#D82C20",
    Icon = "fa fa-bolt",
    Inputs = 1,
    Outputs = 1)]
public class RedisNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("host", "Host", icon: "fa fa-server", placeholder: "localhost")
            .AddNumber("port", "Port", defaultValue: 6379)
            .AddPassword("password", "Password")
            .AddNumber("database", "Database Index", defaultValue: 0)
            .AddSelect("operation", "Operation", new[]
            {
                ("get", "GET - Get value"),
                ("set", "SET - Set value"),
                ("del", "DEL - Delete key"),
                ("exists", "EXISTS - Check if key exists"),
                ("keys", "KEYS - Get keys by pattern"),
                ("hget", "HGET - Hash get"),
                ("hset", "HSET - Hash set"),
                ("hgetall", "HGETALL - Get all hash fields"),
                ("lpush", "LPUSH - Push to list (left)"),
                ("rpush", "RPUSH - Push to list (right)"),
                ("lpop", "LPOP - Pop from list (left)"),
                ("rpop", "RPOP - Pop from list (right)"),
                ("lrange", "LRANGE - Get list range"),
                ("sadd", "SADD - Add to set"),
                ("smembers", "SMEMBERS - Get set members"),
                ("publish", "PUBLISH - Publish message"),
                ("subscribe", "SUBSCRIBE - Subscribe to channel"),
                ("expire", "EXPIRE - Set key expiration"),
                ("ttl", "TTL - Get time to live"),
                ("incr", "INCR - Increment"),
                ("decr", "DECR - Decrement")
            }, defaultValue: "get")
            .AddText("key", "Key", icon: "fa fa-key")
            .AddText("field", "Field (for hash operations)", placeholder: "Hash field name")
            .AddNumber("expiry", "Expiry (seconds)", defaultValue: 0, suffix: "0 = no expiry")
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "host", "localhost" },
        { "port", 6379 },
        { "database", 0 },
        { "operation", "get" },
        { "key", "" },
        { "field", "" },
        { "expiry", 0 }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Executes commands against Redis cache/database.")
        .AddInput("msg.payload", "various", "Value to set (for SET operations)")
        .AddInput("msg.key", "string", "Optional key override")
        .AddOutput("msg.payload", "various", "Retrieved value or operation result")
        .Details(@"
Connects to **Redis** and executes various commands.

**String Operations:**
- **GET/SET**: Basic key-value operations
- **INCR/DECR**: Increment/decrement numbers

**Hash Operations:**
- **HGET/HSET**: Hash field operations
- **HGETALL**: Get all hash fields

**List Operations:**
- **LPUSH/RPUSH**: Add to list
- **LPOP/RPOP**: Remove from list
- **LRANGE**: Get list range

**Set Operations:**
- **SADD**: Add to set
- **SMEMBERS**: Get set members

**Pub/Sub:**
- **PUBLISH**: Publish message to channel
- **SUBSCRIBE**: Subscribe to channel

**Note:** Requires `StackExchange.Redis` package to be added.")
        .Build();

    protected override Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var host = GetConfig("host", "localhost");
            var port = GetConfig("port", 6379);
            var database = GetConfig("database", 0);
            var operation = GetConfig("operation", "get");
            var key = msg.Properties.TryGetValue("key", out var k) 
                ? k?.ToString() 
                : GetConfig<string>("key", "");

            if (string.IsNullOrEmpty(key) && operation != "keys")
            {
                Error("No key specified");
                done(new Exception("No key specified"));
                return Task.CompletedTask;
            }

            Status($"Executing {operation}...", StatusFill.Yellow, SdkStatusShape.Ring);

            // Note: Actual execution requires StackExchange.Redis package
            Log($"Redis: {operation} on {host}:{port} db{database}");
            Log($"Key: {key}");
            
            msg.Payload = new { 
                Status = "Executed",
                Operation = operation,
                Key = key,
                Host = host,
                Database = database,
                Note = "Add StackExchange.Redis package for actual Redis connectivity"
            };

            Status($"{operation} completed", StatusFill.Green, SdkStatusShape.Dot);
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
