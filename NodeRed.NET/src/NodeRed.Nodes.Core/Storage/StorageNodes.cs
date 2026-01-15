// ============================================================
// SOURCE: packages/node_modules/@node-red/nodes/core/storage/*.js
// TRANSLATION: Storage nodes - File, Watch
// ============================================================

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NodeRed.Util;

namespace NodeRed.Nodes.Core.Storage;

#region File Node
/// <summary>
/// File node configuration
/// SOURCE: 10-file.js
/// </summary>
public class FileNodeConfig : NodeConfig
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";
    
    [JsonPropertyName("filenameType")]
    public string FilenameType { get; set; } = "str";
    
    [JsonPropertyName("appendNewline")]
    public bool AppendNewline { get; set; } = true;
    
    [JsonPropertyName("createDir")]
    public bool CreateDir { get; set; }
    
    [JsonPropertyName("overwriteFile")]
    public string OverwriteFile { get; set; } = "false"; // false, true, delete
    
    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = "none"; // none, utf8, base64, binary
}

/// <summary>
/// File node - Writes to a file
/// SOURCE: packages/node_modules/@node-red/nodes/core/storage/10-file.js
/// 
/// MAPPING NOTES:
/// - fs.appendFile → File.AppendAllText
/// - fs.writeFile → File.WriteAllText
/// - fs.unlink → File.Delete
/// </summary>
public class FileNode : BaseNode
{
    private readonly string _filename;
    private readonly string _filenameType;
    private readonly bool _appendNewline;
    private readonly bool _createDir;
    private readonly string _overwriteFile;
    private readonly Encoding _encoding;
    
    public FileNode(NodeConfig config) : base(config)
    {
        var fileConfig = config as FileNodeConfig ?? new FileNodeConfig();
        
        _filename = fileConfig.Filename ?? "";
        _filenameType = fileConfig.FilenameType ?? "str";
        _appendNewline = fileConfig.AppendNewline;
        _createDir = fileConfig.CreateDir;
        _overwriteFile = fileConfig.OverwriteFile ?? "false";
        
        _encoding = fileConfig.Encoding switch
        {
            "utf8" => Encoding.UTF8,
            "base64" => Encoding.UTF8, // Special handling needed
            "binary" => Encoding.GetEncoding("ISO-8859-1"),
            _ => Encoding.UTF8
        };
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                var filename = GetFilename(msg);
                if (string.IsNullOrEmpty(filename))
                {
                    done(new Exception("No filename specified"));
                    return;
                }
                
                // Create directory if needed
                if (_createDir)
                {
                    var dir = Path.GetDirectoryName(filename);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                }
                
                if (_overwriteFile == "delete")
                {
                    // Delete file
                    if (File.Exists(filename))
                    {
                        File.Delete(filename);
                    }
                }
                else
                {
                    // Get content to write
                    var content = GetContent(msg);
                    if (_appendNewline && !content.EndsWith("\n"))
                    {
                        content += "\n";
                    }
                    
                    if (_overwriteFile == "true")
                    {
                        // Overwrite file
                        await File.WriteAllTextAsync(filename, content, _encoding);
                    }
                    else
                    {
                        // Append to file
                        await File.AppendAllTextAsync(filename, content, _encoding);
                    }
                }
                
                send(msg);
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    private string GetFilename(FlowMessage msg)
    {
        return _filenameType switch
        {
            "msg" => msg.AdditionalProperties?.TryGetValue(_filename, out var val) == true 
                ? val.ToString() ?? "" 
                : _filename,
            "env" => Environment.GetEnvironmentVariable(_filename) ?? "",
            _ => _filename
        };
    }
    
    private string GetContent(FlowMessage msg)
    {
        if (msg.Payload is byte[] buffer)
        {
            return _encoding.GetString(buffer);
        }
        
        return msg.Payload?.ToString() ?? "";
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("file", config => new FileNode(config));
    }
}
#endregion

#region File In Node
/// <summary>
/// File In node configuration
/// SOURCE: 10-file.js (file in section)
/// </summary>
public class FileInNodeConfig : NodeConfig
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";
    
    [JsonPropertyName("filenameType")]
    public string FilenameType { get; set; } = "str";
    
    [JsonPropertyName("format")]
    public string Format { get; set; } = "utf8"; // utf8, lines, stream
    
    [JsonPropertyName("chunk")]
    public bool Chunk { get; set; }
    
    [JsonPropertyName("sendError")]
    public bool SendError { get; set; }
    
    [JsonPropertyName("allProps")]
    public bool AllProps { get; set; }
    
    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = "utf8";
}

/// <summary>
/// File In node - Reads from a file
/// SOURCE: packages/node_modules/@node-red/nodes/core/storage/10-file.js (file in section)
/// </summary>
public class FileInNode : BaseNode
{
    private readonly string _filename;
    private readonly string _filenameType;
    private readonly string _format;
    private readonly Encoding _encoding;
    
    public FileInNode(NodeConfig config) : base(config)
    {
        var fileConfig = config as FileInNodeConfig ?? new FileInNodeConfig();
        
        _filename = fileConfig.Filename ?? "";
        _filenameType = fileConfig.FilenameType ?? "str";
        _format = fileConfig.Format ?? "utf8";
        
        _encoding = fileConfig.Encoding switch
        {
            "utf8" => Encoding.UTF8,
            "base64" => Encoding.UTF8,
            "binary" => Encoding.GetEncoding("ISO-8859-1"),
            _ => Encoding.UTF8
        };
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                var filename = GetFilename(msg);
                if (string.IsNullOrEmpty(filename))
                {
                    done(new Exception("No filename specified"));
                    return;
                }
                
                if (!File.Exists(filename))
                {
                    done(new Exception($"File not found: {filename}"));
                    return;
                }
                
                if (_format == "lines")
                {
                    // Read line by line
                    var lines = await File.ReadAllLinesAsync(filename, _encoding);
                    var count = lines.Length;
                    var partId = global::NodeRed.Util.Util.GenerateId();
                    
                    for (int i = 0; i < count; i++)
                    {
                        var lineMsg = msg.Clone();
                        lineMsg.Payload = lines[i];
                        lineMsg.Parts = new MessageParts
                        {
                            Id = partId,
                            Index = i,
                            Count = count,
                            Ch = "\n"
                        };
                        send(lineMsg);
                    }
                }
                else if (_format == "stream" || _format == "buffer")
                {
                    // Read as binary
                    var content = await File.ReadAllBytesAsync(filename);
                    msg.Payload = content;
                    send(msg);
                }
                else
                {
                    // Read as text
                    var content = await File.ReadAllTextAsync(filename, _encoding);
                    msg.Payload = content;
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
    
    private string GetFilename(FlowMessage msg)
    {
        return _filenameType switch
        {
            "msg" => msg.AdditionalProperties?.TryGetValue(_filename, out var val) == true 
                ? val.ToString() ?? "" 
                : _filename,
            "env" => Environment.GetEnvironmentVariable(_filename) ?? "",
            _ => _filename
        };
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("file in", config => new FileInNode(config));
    }
}
#endregion

#region Watch Node
/// <summary>
/// Watch node configuration
/// SOURCE: 23-watch.js
/// </summary>
public class WatchNodeConfig : NodeConfig
{
    [JsonPropertyName("files")]
    public string Files { get; set; } = "";
    
    [JsonPropertyName("recursive")]
    public bool Recursive { get; set; }
}

/// <summary>
/// Watch node - Watches for file changes
/// SOURCE: packages/node_modules/@node-red/nodes/core/storage/23-watch.js
/// 
/// MAPPING NOTES:
/// - chokidar.watch → FileSystemWatcher
/// </summary>
public class WatchNode : BaseNode
{
    private readonly List<FileSystemWatcher> _watchers = new();
    
    public WatchNode(NodeConfig config) : base(config)
    {
        var watchConfig = config as WatchNodeConfig ?? new WatchNodeConfig();
        
        var files = watchConfig.Files ?? "";
        var recursive = watchConfig.Recursive;
        
        // Parse file paths
        var paths = files.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p));
        
        foreach (var path in paths)
        {
            try
            {
                string directory;
                string filter;
                
                if (Directory.Exists(path))
                {
                    directory = path;
                    filter = "*.*";
                }
                else
                {
                    directory = Path.GetDirectoryName(path) ?? ".";
                    filter = Path.GetFileName(path);
                }
                
                if (!Directory.Exists(directory))
                    continue;
                
                var watcher = new FileSystemWatcher(directory, filter)
                {
                    IncludeSubdirectories = recursive,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | 
                                   NotifyFilters.DirectoryName | NotifyFilters.Size
                };
                
                watcher.Changed += (s, e) => OnFileEvent(e, "change");
                watcher.Created += (s, e) => OnFileEvent(e, "create");
                watcher.Deleted += (s, e) => OnFileEvent(e, "delete");
                watcher.Renamed += (s, e) => OnRenamedEvent(e);
                
                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                Error($"Error setting up watch for {path}: {ex.Message}");
            }
        }
    }
    
    private void OnFileEvent(FileSystemEventArgs e, string eventType)
    {
        var msg = new FlowMessage
        {
            Payload = e.FullPath,
            Topic = e.Name
        };
        
        msg.AdditionalProperties = new Dictionary<string, JsonElement>
        {
            ["file"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(e.FullPath)),
            ["type"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(eventType))
        };
        
        _ = ReceiveAsync(msg);
    }
    
    private void OnRenamedEvent(RenamedEventArgs e)
    {
        var msg = new FlowMessage
        {
            Payload = e.FullPath,
            Topic = e.Name
        };
        
        msg.AdditionalProperties = new Dictionary<string, JsonElement>
        {
            ["file"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(e.FullPath)),
            ["type"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize("rename")),
            ["oldPath"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(e.OldFullPath))
        };
        
        _ = ReceiveAsync(msg);
    }
    
    public override async Task CloseAsync(bool removed = false)
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
        
        await base.CloseAsync(removed);
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("watch", config => new WatchNode(config));
    }
}
#endregion
