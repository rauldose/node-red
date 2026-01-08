// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using StackExchange.Redis;
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
    private static readonly Dictionary<string, ConnectionMultiplexer> ConnectionPool = new();
    private static readonly object PoolLock = new();

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
- **PUBLISH**: Publish message to channel")
        .Build();

    protected override async Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        try
        {
            var host = GetConfig("host", "localhost");
            var port = GetConfig("port", 6379);
            var password = GetConfig<string>("password", "");
            var databaseIndex = GetConfig("database", 0);
            var operation = GetConfig("operation", "get");
            var expiry = GetConfig("expiry", 0);
            var key = msg.Properties.TryGetValue("key", out var k) 
                ? k?.ToString() 
                : GetConfig<string>("key", "");
            var field = GetConfig<string>("field", "");

            if (string.IsNullOrEmpty(key) && operation != "keys")
            {
                Error("No key specified");
                done(new Exception("No key specified"));
                return;
            }

            Status($"Executing {operation}...", StatusFill.Yellow, SdkStatusShape.Ring);

            // Get or create connection
            var connectionKey = $"{host}:{port}";
            ConnectionMultiplexer connection;
            
            lock (PoolLock)
            {
                if (!ConnectionPool.TryGetValue(connectionKey, out connection!) || !connection.IsConnected)
                {
                    var config = new ConfigurationOptions
                    {
                        EndPoints = { { host, port } },
                        AbortOnConnectFail = false,
                        ConnectTimeout = 5000
                    };
                    if (!string.IsNullOrEmpty(password))
                    {
                        config.Password = password;
                    }
                    connection = ConnectionMultiplexer.Connect(config);
                    ConnectionPool[connectionKey] = connection;
                }
            }

            var db = connection.GetDatabase(databaseIndex);
            var redisKey = new RedisKey(key);

            switch (operation)
            {
                case "get":
                    var getValue = await db.StringGetAsync(redisKey);
                    msg.Payload = getValue.HasValue ? ParseRedisValue(getValue!) : null;
                    break;

                case "set":
                    var setValue = SerializeValue(msg.Payload);
                    TimeSpan? expirySpan = expiry > 0 ? TimeSpan.FromSeconds(expiry) : null;
                    var setResult = await db.StringSetAsync(redisKey, setValue, expirySpan);
                    msg.Payload = new Dictionary<string, object?> { { "success", setResult } };
                    break;

                case "del":
                    var delResult = await db.KeyDeleteAsync(redisKey);
                    msg.Payload = new Dictionary<string, object?> { { "deleted", delResult } };
                    break;

                case "exists":
                    var existsResult = await db.KeyExistsAsync(redisKey);
                    msg.Payload = existsResult;
                    break;

                case "keys":
                    var server = connection.GetServer(host, port);
                    var keys = server.Keys(databaseIndex, key ?? "*").Select(k => k.ToString()).ToList();
                    msg.Payload = keys;
                    break;

                case "hget":
                    var hgetValue = await db.HashGetAsync(redisKey, field);
                    msg.Payload = hgetValue.HasValue ? ParseRedisValue(hgetValue!) : null;
                    break;

                case "hset":
                    var hsetValue = SerializeValue(msg.Payload);
                    var hsetResult = await db.HashSetAsync(redisKey, field, hsetValue);
                    msg.Payload = new Dictionary<string, object?> { { "created", hsetResult } };
                    break;

                case "hgetall":
                    var hashEntries = await db.HashGetAllAsync(redisKey);
                    msg.Payload = hashEntries.ToDictionary(
                        e => e.Name.ToString(),
                        e => ParseRedisValue(e.Value!));
                    break;

                case "lpush":
                    var lpushValue = SerializeValue(msg.Payload);
                    var lpushLength = await db.ListLeftPushAsync(redisKey, lpushValue);
                    msg.Payload = new Dictionary<string, object?> { { "length", lpushLength } };
                    break;

                case "rpush":
                    var rpushValue = SerializeValue(msg.Payload);
                    var rpushLength = await db.ListRightPushAsync(redisKey, rpushValue);
                    msg.Payload = new Dictionary<string, object?> { { "length", rpushLength } };
                    break;

                case "lpop":
                    var lpopValue = await db.ListLeftPopAsync(redisKey);
                    msg.Payload = lpopValue.HasValue ? ParseRedisValue(lpopValue!) : null;
                    break;

                case "rpop":
                    var rpopValue = await db.ListRightPopAsync(redisKey);
                    msg.Payload = rpopValue.HasValue ? ParseRedisValue(rpopValue!) : null;
                    break;

                case "lrange":
                    var start = msg.Properties.TryGetValue("start", out var s) ? Convert.ToInt64(s) : 0;
                    var stop = msg.Properties.TryGetValue("stop", out var st) ? Convert.ToInt64(st) : -1;
                    var rangeValues = await db.ListRangeAsync(redisKey, start, stop);
                    msg.Payload = rangeValues.Select(v => ParseRedisValue(v!)).ToList();
                    break;

                case "sadd":
                    var saddValue = SerializeValue(msg.Payload);
                    var saddResult = await db.SetAddAsync(redisKey, saddValue);
                    msg.Payload = new Dictionary<string, object?> { { "added", saddResult } };
                    break;

                case "smembers":
                    var members = await db.SetMembersAsync(redisKey);
                    msg.Payload = members.Select(m => ParseRedisValue(m!)).ToList();
                    break;

                case "publish":
                    var channel = new RedisChannel(key!, RedisChannel.PatternMode.Literal);
                    var pubValue = SerializeValue(msg.Payload);
                    var subscribers = await db.PublishAsync(channel, pubValue);
                    msg.Payload = new Dictionary<string, object?> { { "subscribers", subscribers } };
                    break;

                case "expire":
                    var expireResult = await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(expiry));
                    msg.Payload = new Dictionary<string, object?> { { "success", expireResult } };
                    break;

                case "ttl":
                    var ttl = await db.KeyTimeToLiveAsync(redisKey);
                    msg.Payload = ttl?.TotalSeconds ?? -1;
                    break;

                case "incr":
                    var incrResult = await db.StringIncrementAsync(redisKey);
                    msg.Payload = incrResult;
                    break;

                case "decr":
                    var decrResult = await db.StringDecrementAsync(redisKey);
                    msg.Payload = decrResult;
                    break;
            }

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
    }

    private static object? ParseRedisValue(RedisValue value)
    {
        var str = value.ToString();
        
        // Try to parse as JSON
        if (str.StartsWith("{") || str.StartsWith("["))
        {
            try
            {
                return JsonSerializer.Deserialize<object>(str);
            }
            catch (JsonException)
            {
                // Not valid JSON, return as string
            }
        }
        
        // Try to parse as number
        if (long.TryParse(str, out var longVal))
            return longVal;
        if (double.TryParse(str, out var doubleVal))
            return doubleVal;
        if (bool.TryParse(str, out var boolVal))
            return boolVal;
            
        return str;
    }

    private static RedisValue SerializeValue(object? value)
    {
        if (value == null) return RedisValue.Null;
        if (value is string s) return s;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is double d) return d;
        if (value is bool b) return b ? "true" : "false";
        
        // Serialize complex objects as JSON
        return JsonSerializer.Serialize(value);
    }
}
