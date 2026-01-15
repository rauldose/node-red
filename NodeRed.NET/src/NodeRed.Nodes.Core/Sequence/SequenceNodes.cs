// ============================================================
// SOURCE: packages/node_modules/@node-red/nodes/core/sequence/*.js
// TRANSLATION: Sequence nodes - Split, Join, Sort, Batch
// ============================================================

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NodeRed.Util;

namespace NodeRed.Nodes.Core.Sequence;

#region Split Node
/// <summary>
/// Split node configuration
/// SOURCE: 17-split.js
/// </summary>
public class SplitNodeConfig : NodeConfig
{
    [JsonPropertyName("splt")]
    public string Splt { get; set; } = "\\n";
    
    [JsonPropertyName("spltType")]
    public string SpltType { get; set; } = "str";
    
    [JsonPropertyName("arraySplt")]
    public int ArraySplt { get; set; } = 1;
    
    [JsonPropertyName("arraySpltType")]
    public string ArraySpltType { get; set; } = "len";
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
    
    [JsonPropertyName("addname")]
    public string? AddName { get; set; }
}

/// <summary>
/// Split node - Splits a message into a sequence
/// SOURCE: packages/node_modules/@node-red/nodes/core/sequence/17-split.js
/// 
/// MAPPING NOTES:
/// - Handles strings, arrays, objects, buffers
/// - Generates msg.parts for sequence tracking
/// </summary>
public class SplitNode : BaseNode
{
    private readonly string _splitter;
    private readonly string _splitterType;
    private readonly int _arrayChunkSize;
    private readonly bool _stream;
    private readonly string? _addName;
    
    public SplitNode(NodeConfig config) : base(config)
    {
        var splitConfig = config as SplitNodeConfig ?? new SplitNodeConfig();
        
        _splitter = splitConfig.Splt ?? "\\n";
        _splitterType = splitConfig.SpltType ?? "str";
        _arrayChunkSize = splitConfig.ArraySplt > 0 ? splitConfig.ArraySplt : 1;
        _stream = splitConfig.Stream;
        _addName = splitConfig.AddName;
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                var payload = msg.Payload;
                var parts = new List<object?>();
                var partId = global::NodeRed.Util.Util.GenerateId();
                
                if (payload is string str)
                {
                    // Split string
                    var delimiter = ParseDelimiter(_splitter, _splitterType);
                    var items = str.Split(new[] { delimiter }, StringSplitOptions.None);
                    parts.AddRange(items.Cast<object?>());
                }
                else if (payload is byte[] buffer)
                {
                    // Split buffer
                    var chunkSize = _arrayChunkSize;
                    for (int i = 0; i < buffer.Length; i += chunkSize)
                    {
                        var chunk = new byte[Math.Min(chunkSize, buffer.Length - i)];
                        Array.Copy(buffer, i, chunk, 0, chunk.Length);
                        parts.Add(chunk);
                    }
                }
                else if (payload is Array arr)
                {
                    // Split array
                    for (int i = 0; i < arr.Length; i += _arrayChunkSize)
                    {
                        if (_arrayChunkSize == 1)
                        {
                            parts.Add(arr.GetValue(i));
                        }
                        else
                        {
                            var chunk = new object?[Math.Min(_arrayChunkSize, arr.Length - i)];
                            for (int j = 0; j < chunk.Length; j++)
                            {
                                chunk[j] = arr.GetValue(i + j);
                            }
                            parts.Add(chunk);
                        }
                    }
                }
                else if (payload is JsonElement je)
                {
                    if (je.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in je.EnumerateArray())
                        {
                            parts.Add(item);
                        }
                    }
                    else if (je.ValueKind == JsonValueKind.Object)
                    {
                        // Split object into key-value pairs
                        foreach (var prop in je.EnumerateObject())
                        {
                            var obj = new Dictionary<string, object?> { [prop.Name] = prop.Value };
                            parts.Add(obj);
                        }
                    }
                    else
                    {
                        parts.Add(payload);
                    }
                }
                else if (payload is IDictionary<string, object?> dict)
                {
                    // Split object into key-value pairs
                    foreach (var kv in dict)
                    {
                        var obj = new Dictionary<string, object?> { [kv.Key] = kv.Value };
                        parts.Add(obj);
                    }
                }
                else
                {
                    parts.Add(payload);
                }
                
                // Send each part
                var count = parts.Count;
                for (int i = 0; i < count; i++)
                {
                    var partMsg = msg.Clone();
                    partMsg.Payload = parts[i];
                    partMsg.Parts = new MessageParts
                    {
                        Id = partId,
                        Index = i,
                        Count = count,
                        Type = GetPayloadType(payload),
                        Ch = _splitter
                    };
                    
                    if (!string.IsNullOrEmpty(_addName))
                    {
                        // Add property name for object splits
                        partMsg.AdditionalProperties ??= new Dictionary<string, JsonElement>();
                        if (parts[i] is Dictionary<string, object?> d && d.Count == 1)
                        {
                            var key = d.Keys.First();
                            partMsg.AdditionalProperties[_addName] = JsonSerializer.Deserialize<JsonElement>(
                                JsonSerializer.Serialize(key));
                        }
                    }
                    
                    send(partMsg);
                }
                
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private string ParseDelimiter(string splitter, string type)
    {
        if (type == "bin")
        {
            // Binary delimiter - parse as bytes
            return Encoding.UTF8.GetString(Convert.FromBase64String(splitter));
        }
        
        // Handle escape sequences
        return splitter
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t");
    }
    
    private string GetPayloadType(object? payload)
    {
        return payload switch
        {
            string => "string",
            byte[] => "buffer",
            Array => "array",
            IDictionary<string, object?> => "object",
            JsonElement je when je.ValueKind == JsonValueKind.Array => "array",
            JsonElement je when je.ValueKind == JsonValueKind.Object => "object",
            _ => "string"
        };
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("split", config => new SplitNode(config));
    }
}
#endregion

#region Join Node
/// <summary>
/// Join node configuration
/// SOURCE: 17-split.js (join section)
/// </summary>
public class JoinNodeConfig : NodeConfig
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "auto"; // auto, custom, reduce
    
    [JsonPropertyName("build")]
    public string Build { get; set; } = "string"; // string, array, object, buffer, merged
    
    [JsonPropertyName("property")]
    public string Property { get; set; } = "payload";
    
    [JsonPropertyName("propertyType")]
    public string PropertyType { get; set; } = "msg";
    
    [JsonPropertyName("key")]
    public string Key { get; set; } = "topic";
    
    [JsonPropertyName("joiner")]
    public string Joiner { get; set; } = "\\n";
    
    [JsonPropertyName("joinerType")]
    public string JoinerType { get; set; } = "str";
    
    [JsonPropertyName("accumulate")]
    public bool Accumulate { get; set; }
    
    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 0;
    
    [JsonPropertyName("count")]
    public int Count { get; set; } = 0;
}

/// <summary>
/// Join node - Joins a sequence into a single message
/// SOURCE: packages/node_modules/@node-red/nodes/core/sequence/17-split.js (join section)
/// </summary>
public class JoinNode : BaseNode
{
    private readonly string _mode;
    private readonly string _build;
    private readonly string _key;
    private readonly string _joiner;
    private readonly bool _accumulate;
    private readonly int _timeout;
    private readonly int _count;
    
    private readonly ConcurrentDictionary<string, JoinGroup> _groups = new();
    
    public JoinNode(NodeConfig config) : base(config)
    {
        var joinConfig = config as JoinNodeConfig ?? new JoinNodeConfig();
        
        _mode = joinConfig.Mode ?? "auto";
        _build = joinConfig.Build ?? "string";
        _key = joinConfig.Key ?? "topic";
        _joiner = ParseJoiner(joinConfig.Joiner ?? "\\n", joinConfig.JoinerType ?? "str");
        _accumulate = joinConfig.Accumulate;
        _timeout = joinConfig.Timeout * 1000;
        _count = joinConfig.Count;
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                // Get group ID from msg.parts or topic
                var groupId = msg.Parts?.Id ?? msg.Topic ?? "default";
                var parts = msg.Parts;
                
                // Get or create group
                var group = _groups.GetOrAdd(groupId, _ => new JoinGroup
                {
                    Id = groupId,
                    Parts = new List<(int index, object? payload)>(),
                    Count = parts?.Count,
                    Type = parts?.Type ?? _build,
                    Ch = parts?.Ch ?? _joiner,
                    Msg = msg
                });
                
                lock (group)
                {
                    // Add this part
                    var index = parts?.Index ?? group.Parts.Count;
                    group.Parts.Add((index, msg.Payload));
                    
                    // Check if complete
                    bool complete = false;
                    
                    if (_mode == "auto" && parts != null)
                    {
                        // Complete when all parts received
                        complete = parts.Count.HasValue && group.Parts.Count >= parts.Count.Value;
                    }
                    else if (_count > 0)
                    {
                        complete = group.Parts.Count >= _count;
                    }
                    else if (parts?.Count.HasValue == true)
                    {
                        complete = group.Parts.Count >= parts.Count.Value;
                    }
                    
                    if (complete)
                    {
                        // Build and send result
                        var result = BuildResult(group);
                        var outMsg = msg.Clone();
                        outMsg.Payload = result;
                        outMsg.Parts = null; // Remove parts
                        
                        _groups.TryRemove(groupId, out _);
                        send(outMsg);
                    }
                    else if (_timeout > 0)
                    {
                        // Set timeout for group
                        if (group.TimeoutTimer == null)
                        {
                            group.TimeoutTimer = SetTimeout(() =>
                            {
                                if (_groups.TryRemove(groupId, out var g))
                                {
                                    var result = BuildResult(g);
                                    var outMsg = g.Msg?.Clone() ?? new FlowMessage();
                                    outMsg.Payload = result;
                                    send(outMsg);
                                }
                            }, _timeout);
                        }
                    }
                }
                
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private string ParseJoiner(string joiner, string type)
    {
        if (type == "bin")
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(joiner));
        }
        
        return joiner
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t");
    }
    
    private object? BuildResult(JoinGroup group)
    {
        // Sort by index
        var sorted = group.Parts.OrderBy(p => p.index).Select(p => p.payload).ToList();
        
        return group.Type switch
        {
            "string" => string.Join(group.Ch ?? "", sorted.Select(s => s?.ToString() ?? "")),
            "buffer" => ConcatenateBuffers(sorted),
            "array" => sorted,
            "object" => BuildObject(sorted, group),
            "merged" => MergeObjects(sorted),
            _ => sorted
        };
    }
    
    private byte[] ConcatenateBuffers(List<object?> items)
    {
        var buffers = items.Where(i => i is byte[]).Cast<byte[]>().ToList();
        var totalLength = buffers.Sum(b => b.Length);
        var result = new byte[totalLength];
        var offset = 0;
        foreach (var buffer in buffers)
        {
            Array.Copy(buffer, 0, result, offset, buffer.Length);
            offset += buffer.Length;
        }
        return result;
    }
    
    private Dictionary<string, object?> BuildObject(List<object?> items, JoinGroup group)
    {
        var result = new Dictionary<string, object?>();
        
        foreach (var item in items)
        {
            if (item is IDictionary<string, object?> dict)
            {
                foreach (var kv in dict)
                {
                    result[kv.Key] = kv.Value;
                }
            }
        }
        
        return result;
    }
    
    private Dictionary<string, object?> MergeObjects(List<object?> items)
    {
        var result = new Dictionary<string, object?>();
        
        foreach (var item in items)
        {
            if (item is IDictionary<string, object?> dict)
            {
                foreach (var kv in dict)
                {
                    result[kv.Key] = kv.Value;
                }
            }
            else if (item is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in je.EnumerateObject())
                {
                    result[prop.Name] = prop.Value;
                }
            }
        }
        
        return result;
    }
    
    public override async Task CloseAsync(bool removed = false)
    {
        foreach (var group in _groups.Values)
        {
            if (group.TimeoutTimer != null)
            {
                ClearTimeout(group.TimeoutTimer);
            }
        }
        _groups.Clear();
        
        await base.CloseAsync(removed);
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("join", config => new JoinNode(config));
    }
    
    private class JoinGroup
    {
        public string Id { get; set; } = "";
        public List<(int index, object? payload)> Parts { get; set; } = new();
        public int? Count { get; set; }
        public string? Type { get; set; }
        public string? Ch { get; set; }
        public FlowMessage? Msg { get; set; }
        public Timer? TimeoutTimer { get; set; }
    }
}
#endregion

#region Sort Node
/// <summary>
/// Sort node configuration
/// SOURCE: 18-sort.js
/// </summary>
public class SortNodeConfig : NodeConfig
{
    [JsonPropertyName("target")]
    public string Target { get; set; } = "payload";
    
    [JsonPropertyName("targetType")]
    public string TargetType { get; set; } = "msg";
    
    [JsonPropertyName("msgKey")]
    public string MsgKey { get; set; } = "payload";
    
    [JsonPropertyName("msgKeyType")]
    public string MsgKeyType { get; set; } = "elem";
    
    [JsonPropertyName("seqKey")]
    public string SeqKey { get; set; } = "payload";
    
    [JsonPropertyName("seqKeyType")]
    public string SeqKeyType { get; set; } = "msg";
    
    [JsonPropertyName("order")]
    public string Order { get; set; } = "ascending";
    
    [JsonPropertyName("as_num")]
    public bool AsNum { get; set; }
}

/// <summary>
/// Sort node - Sorts array or message sequence
/// SOURCE: packages/node_modules/@node-red/nodes/core/sequence/18-sort.js
/// </summary>
public class SortNode : BaseNode
{
    private readonly string _target;
    private readonly string _order;
    private readonly bool _asNum;
    private readonly string _msgKey;
    
    private readonly ConcurrentDictionary<string, SortGroup> _groups = new();
    
    public SortNode(NodeConfig config) : base(config)
    {
        var sortConfig = config as SortNodeConfig ?? new SortNodeConfig();
        
        _target = sortConfig.Target ?? "payload";
        _order = sortConfig.Order ?? "ascending";
        _asNum = sortConfig.AsNum;
        _msgKey = sortConfig.SeqKey ?? "payload";
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                var payload = msg.Payload;
                
                // Check if this is a sequence
                if (msg.Parts?.Id != null)
                {
                    // Sort as sequence
                    ProcessSequence(msg, send);
                }
                else if (payload is Array arr || 
                         (payload is JsonElement je && je.ValueKind == JsonValueKind.Array))
                {
                    // Sort array in place
                    var sorted = SortArray(payload);
                    msg.Payload = sorted;
                    send(msg);
                }
                else
                {
                    // Pass through
                    send(msg);
                }
                
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private List<object?> SortArray(object? payload)
    {
        var items = new List<object?>();
        
        if (payload is Array arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                items.Add(arr.GetValue(i));
            }
        }
        else if (payload is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in je.EnumerateArray())
            {
                items.Add(item);
            }
        }
        
        items.Sort((a, b) =>
        {
            var result = CompareValues(a, b);
            return _order == "descending" ? -result : result;
        });
        
        return items;
    }
    
    private int CompareValues(object? a, object? b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;
        
        if (_asNum)
        {
            var numA = ConvertToNumber(a);
            var numB = ConvertToNumber(b);
            return numA.CompareTo(numB);
        }
        
        return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }
    
    private double ConvertToNumber(object? value)
    {
        if (value == null) return 0;
        if (value is double d) return d;
        if (value is int i) return i;
        if (value is long l) return l;
        if (double.TryParse(value.ToString(), out var num)) return num;
        return 0;
    }
    
    private void ProcessSequence(FlowMessage msg, Action<object?> send)
    {
        var groupId = msg.Parts!.Id!;
        
        var group = _groups.GetOrAdd(groupId, _ => new SortGroup
        {
            Count = msg.Parts!.Count,
            Messages = new List<FlowMessage>()
        });
        
        lock (group)
        {
            group.Messages.Add(msg);
            
            if (group.Count.HasValue && group.Messages.Count >= group.Count.Value)
            {
                // Sort and send
                var sorted = group.Messages
                    .OrderBy(m =>
                    {
                        var val = GetSortKey(m);
                        if (_asNum && double.TryParse(val?.ToString(), out var num))
                            return (object)num;
                        return val;
                    })
                    .ToList();
                
                if (_order == "descending")
                    sorted.Reverse();
                
                for (int i = 0; i < sorted.Count; i++)
                {
                    var outMsg = sorted[i];
                    outMsg.Parts = new MessageParts
                    {
                        Id = groupId,
                        Index = i,
                        Count = sorted.Count
                    };
                    send(outMsg);
                }
                
                _groups.TryRemove(groupId, out _);
            }
        }
    }
    
    private object? GetSortKey(FlowMessage msg)
    {
        return _msgKey switch
        {
            "payload" => msg.Payload,
            "topic" => msg.Topic,
            _ => msg.AdditionalProperties?.TryGetValue(_msgKey, out var val) == true ? val : null
        };
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("sort", config => new SortNode(config));
    }
    
    private class SortGroup
    {
        public int? Count { get; set; }
        public List<FlowMessage> Messages { get; set; } = new();
    }
}
#endregion

#region Batch Node
/// <summary>
/// Batch node configuration
/// SOURCE: 19-batch.js
/// </summary>
public class BatchNodeConfig : NodeConfig
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "count"; // count, interval, concat
    
    [JsonPropertyName("count")]
    public int Count { get; set; } = 10;
    
    [JsonPropertyName("overlap")]
    public int Overlap { get; set; } = 0;
    
    [JsonPropertyName("interval")]
    public int Interval { get; set; } = 10;
    
    [JsonPropertyName("allowEmptySequence")]
    public bool AllowEmptySequence { get; set; }
    
    [JsonPropertyName("topics")]
    public List<BatchTopic>? Topics { get; set; }
}

/// <summary>
/// Batch topic configuration
/// </summary>
public class BatchTopic
{
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = "";
    
    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;
}

/// <summary>
/// Batch node - Groups messages into batches
/// SOURCE: packages/node_modules/@node-red/nodes/core/sequence/19-batch.js
/// </summary>
public class BatchNode : BaseNode
{
    private readonly string _mode;
    private readonly int _count;
    private readonly int _overlap;
    private readonly int _interval;
    private readonly bool _allowEmpty;
    
    private readonly List<FlowMessage> _batch = new();
    private Timer? _intervalTimer;
    
    public BatchNode(NodeConfig config) : base(config)
    {
        var batchConfig = config as BatchNodeConfig ?? new BatchNodeConfig();
        
        _mode = batchConfig.Mode ?? "count";
        _count = batchConfig.Count > 0 ? batchConfig.Count : 10;
        _overlap = batchConfig.Overlap;
        _interval = batchConfig.Interval * 1000;
        _allowEmpty = batchConfig.AllowEmptySequence;
        
        if (_mode == "interval" && _interval > 0)
        {
            _intervalTimer = SetInterval(() => SendBatch(null), _interval);
        }
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                lock (_batch)
                {
                    _batch.Add(msg);
                    
                    if (_mode == "count" && _batch.Count >= _count)
                    {
                        SendBatch(send);
                    }
                }
                
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private void SendBatch(Action<object?>? send)
    {
        lock (_batch)
        {
            if (_batch.Count == 0 && !_allowEmpty)
                return;
            
            var batchId = global::NodeRed.Util.Util.GenerateId();
            var count = _batch.Count;
            
            for (int i = 0; i < count; i++)
            {
                var msg = _batch[i];
                msg.Parts = new MessageParts
                {
                    Id = batchId,
                    Index = i,
                    Count = count
                };
                
                send?.Invoke(msg);
            }
            
            // Handle overlap
            if (_overlap > 0 && _overlap < _batch.Count)
            {
                var keep = _batch.Skip(_batch.Count - _overlap).ToList();
                _batch.Clear();
                _batch.AddRange(keep);
            }
            else
            {
                _batch.Clear();
            }
        }
    }
    
    public override async Task CloseAsync(bool removed = false)
    {
        if (_intervalTimer != null)
        {
            ClearInterval(_intervalTimer);
            _intervalTimer = null;
        }
        
        // Send any remaining messages
        SendBatch(null);
        
        await base.CloseAsync(removed);
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("batch", config => new BatchNode(config));
    }
}
#endregion
