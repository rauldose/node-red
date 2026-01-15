// ============================================================
// SOURCE: packages/node_modules/@node-red/nodes/core/network/*.js
// TRANSLATION: Network nodes - HTTP, TCP, UDP, WebSocket, MQTT
// ============================================================

using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NodeRed.Util;

namespace NodeRed.Nodes.Core.Network;

#region HTTP Request Node
/// <summary>
/// HTTP Request node configuration
/// SOURCE: 21-httprequest.js
/// </summary>
public class HttpRequestNodeConfig : NodeConfig
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "GET";
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
    
    [JsonPropertyName("ret")]
    public string Ret { get; set; } = "txt"; // txt, bin, obj
    
    [JsonPropertyName("paytoqs")]
    public string PayToQs { get; set; } = "ignore"; // ignore, query, body
    
    [JsonPropertyName("persist")]
    public bool Persist { get; set; }
    
    [JsonPropertyName("tls")]
    public string? Tls { get; set; }
    
    [JsonPropertyName("proxy")]
    public string? Proxy { get; set; }
    
    [JsonPropertyName("insecureHTTPParser")]
    public bool InsecureHttpParser { get; set; }
}

/// <summary>
/// HTTP Request node - Makes HTTP requests
/// SOURCE: packages/node_modules/@node-red/nodes/core/network/21-httprequest.js
/// 
/// MAPPING NOTES:
/// - request() → HttpClient
/// - got library → HttpClient
/// </summary>
public class HttpRequestNode : BaseNode
{
    private readonly string _method;
    private readonly string _url;
    private readonly string _ret;
    private readonly string _payToQs;
    private static readonly HttpClient SharedClient = new();
    
    public HttpRequestNode(NodeConfig config) : base(config)
    {
        var httpConfig = config as HttpRequestNodeConfig ?? new HttpRequestNodeConfig();
        
        _method = httpConfig.Method ?? "GET";
        _url = httpConfig.Url ?? "";
        _ret = httpConfig.Ret ?? "txt";
        _payToQs = httpConfig.PayToQs ?? "ignore";
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                // Get URL from config or message
                var url = string.IsNullOrEmpty(_url) 
                    ? msg.AdditionalProperties?.TryGetValue("url", out var u) == true ? u.ToString() : ""
                    : _url;
                
                // Get method from config or message
                var method = _method == "use" && msg.AdditionalProperties?.TryGetValue("method", out var m) == true
                    ? m.ToString() ?? "GET"
                    : _method;
                
                if (string.IsNullOrEmpty(url))
                {
                    done(new Exception("No URL specified"));
                    return;
                }
                
                // Build request
                var requestUri = new Uri(url);
                
                // Handle query string from payload
                if (_payToQs == "query" && msg.Payload != null)
                {
                    var queryString = BuildQueryString(msg.Payload);
                    var uriBuilder = new UriBuilder(requestUri);
                    if (!string.IsNullOrEmpty(uriBuilder.Query))
                        uriBuilder.Query = uriBuilder.Query.TrimStart('?') + "&" + queryString;
                    else
                        uriBuilder.Query = queryString;
                    requestUri = uriBuilder.Uri;
                }
                
                using var request = new HttpRequestMessage(new HttpMethod(method.ToUpper()), requestUri);
                
                // Add body for POST/PUT/PATCH
                if (_payToQs == "body" || (method.ToUpper() != "GET" && method.ToUpper() != "HEAD" && msg.Payload != null))
                {
                    if (msg.Payload is byte[] bytes)
                    {
                        request.Content = new ByteArrayContent(bytes);
                    }
                    else if (msg.Payload is string str)
                    {
                        request.Content = new StringContent(str, Encoding.UTF8);
                    }
                    else
                    {
                        request.Content = new StringContent(
                            JsonSerializer.Serialize(msg.Payload),
                            Encoding.UTF8,
                            "application/json"
                        );
                    }
                }
                
                // Add headers from message
                if (msg.AdditionalProperties?.TryGetValue("headers", out var headers) == true)
                {
                    if (headers.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var header in headers.EnumerateObject())
                        {
                            request.Headers.TryAddWithoutValidation(header.Name, header.Value.ToString());
                        }
                    }
                }
                
                // Send request
                var response = await SharedClient.SendAsync(request);
                
                // Build response message
                msg.AdditionalProperties ??= new Dictionary<string, JsonElement>();
                msg.AdditionalProperties["statusCode"] = JsonSerializer.Deserialize<JsonElement>(
                    JsonSerializer.Serialize((int)response.StatusCode));
                
                // Get response headers
                var responseHeaders = new Dictionary<string, string>();
                foreach (var header in response.Headers)
                {
                    responseHeaders[header.Key.ToLower()] = string.Join(", ", header.Value);
                }
                foreach (var header in response.Content.Headers)
                {
                    responseHeaders[header.Key.ToLower()] = string.Join(", ", header.Value);
                }
                msg.AdditionalProperties["headers"] = JsonSerializer.Deserialize<JsonElement>(
                    JsonSerializer.Serialize(responseHeaders));
                
                // Get response body
                if (_ret == "bin")
                {
                    msg.Payload = await response.Content.ReadAsByteArrayAsync();
                }
                else if (_ret == "obj")
                {
                    var json = await response.Content.ReadAsStringAsync();
                    try
                    {
                        msg.Payload = JsonSerializer.Deserialize<JsonElement>(json);
                    }
                    catch
                    {
                        msg.Payload = json;
                    }
                }
                else
                {
                    msg.Payload = await response.Content.ReadAsStringAsync();
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
    
    private string BuildQueryString(object? payload)
    {
        if (payload == null) return "";
        
        if (payload is IDictionary<string, object?> dict)
        {
            return string.Join("&", dict.Select(kv => 
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value?.ToString() ?? "")}"));
        }
        
        if (payload is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            return string.Join("&", je.EnumerateObject().Select(p =>
                $"{Uri.EscapeDataString(p.Name)}={Uri.EscapeDataString(p.Value.ToString())}"));
        }
        
        return Uri.EscapeDataString(payload.ToString() ?? "");
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("http request", config => new HttpRequestNode(config));
    }
}
#endregion

#region HTTP In Node
/// <summary>
/// HTTP In node configuration
/// SOURCE: 21-httpin.js
/// </summary>
public class HttpInNodeConfig : NodeConfig
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
    
    [JsonPropertyName("method")]
    public string Method { get; set; } = "get";
    
    [JsonPropertyName("upload")]
    public bool Upload { get; set; }
    
    [JsonPropertyName("swaggerDoc")]
    public object? SwaggerDoc { get; set; }
}

/// <summary>
/// HTTP In node - Receives HTTP requests
/// SOURCE: packages/node_modules/@node-red/nodes/core/network/21-httpin.js
/// 
/// NOTE: This node requires integration with ASP.NET Core's routing.
/// The node registers its URL/method with the HTTP server.
/// </summary>
public class HttpInNode : BaseNode
{
    private readonly string _url;
    private readonly string _method;
    private readonly bool _upload;
    
    /// <summary>
    /// Registry of HTTP endpoints for integration with ASP.NET Core
    /// </summary>
    public static readonly Dictionary<string, (string method, HttpInNode node)> Endpoints = new();
    
    public HttpInNode(NodeConfig config) : base(config)
    {
        var httpConfig = config as HttpInNodeConfig ?? new HttpInNodeConfig();
        
        _url = httpConfig.Url ?? "/";
        _method = httpConfig.Method?.ToUpper() ?? "GET";
        _upload = httpConfig.Upload;
        
        // Register endpoint
        var key = $"{_method}:{_url}";
        Endpoints[key] = (_method, this);
        
        OnInput(async (msg, send, done) =>
        {
            // The HTTP In node doesn't process input normally
            // It receives HTTP requests from the web server
            send(msg);
            done(null);
        });
    }
    
    /// <summary>
    /// Handle an incoming HTTP request
    /// Called by ASP.NET Core integration
    /// </summary>
    public async Task HandleRequestAsync(
        string method, 
        string url, 
        Dictionary<string, string> headers,
        Dictionary<string, string> query,
        object? body,
        Func<FlowMessage, Task> respond)
    {
        var msg = new FlowMessage
        {
            Payload = body,
            Topic = url
        };
        
        msg.AdditionalProperties = new Dictionary<string, JsonElement>
        {
            ["req"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(new
            {
                method,
                url,
                headers,
                query,
                body
            })),
            ["res"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(new { _respond = true }))
        };
        
        // Store respond callback
        _pendingResponses[msg.MsgId] = respond;
        
        await ReceiveAsync(msg);
    }
    
    private readonly Dictionary<string, Func<FlowMessage, Task>> _pendingResponses = new();
    
    public async Task SendResponseAsync(FlowMessage msg)
    {
        if (_pendingResponses.TryGetValue(msg.MsgId, out var respond))
        {
            _pendingResponses.Remove(msg.MsgId);
            await respond(msg);
        }
    }
    
    public override async Task CloseAsync(bool removed = false)
    {
        var key = $"{_method}:{_url}";
        Endpoints.Remove(key);
        await base.CloseAsync(removed);
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("http in", config => new HttpInNode(config));
    }
}
#endregion

#region HTTP Response Node
/// <summary>
/// HTTP Response node configuration
/// SOURCE: 21-httpin.js (response section)
/// </summary>
public class HttpResponseNodeConfig : NodeConfig
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; } = 200;
    
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }
}

/// <summary>
/// HTTP Response node - Sends HTTP response
/// SOURCE: packages/node_modules/@node-red/nodes/core/network/21-httpin.js (response section)
/// </summary>
public class HttpResponseNode : BaseNode
{
    private readonly int _statusCode;
    private readonly Dictionary<string, string>? _headers;
    
    public HttpResponseNode(NodeConfig config) : base(config)
    {
        var httpConfig = config as HttpResponseNodeConfig ?? new HttpResponseNodeConfig();
        
        _statusCode = httpConfig.StatusCode > 0 ? httpConfig.StatusCode : 200;
        _headers = httpConfig.Headers;
        
        OnInput(async (msg, send, done) =>
        {
            // Set status code
            var statusCode = msg.AdditionalProperties?.TryGetValue("statusCode", out var sc) == true
                ? sc.GetInt32()
                : _statusCode;
            
            msg.AdditionalProperties ??= new Dictionary<string, JsonElement>();
            msg.AdditionalProperties["statusCode"] = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(statusCode));
            
            // Merge headers
            if (_headers != null)
            {
                if (msg.AdditionalProperties.TryGetValue("headers", out var existingHeaders))
                {
                    var combined = new Dictionary<string, string>(_headers);
                    if (existingHeaders.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in existingHeaders.EnumerateObject())
                        {
                            combined[prop.Name] = prop.Value.ToString();
                        }
                    }
                    msg.AdditionalProperties["headers"] = JsonSerializer.Deserialize<JsonElement>(
                        JsonSerializer.Serialize(combined));
                }
                else
                {
                    msg.AdditionalProperties["headers"] = JsonSerializer.Deserialize<JsonElement>(
                        JsonSerializer.Serialize(_headers));
                }
            }
            
            send(msg);
            done(null);
        });
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("http response", config => new HttpResponseNode(config));
    }
}
#endregion

#region TCP Nodes
/// <summary>
/// TCP In node configuration
/// SOURCE: 31-tcpin.js
/// </summary>
public class TcpInNodeConfig : NodeConfig
{
    [JsonPropertyName("server")]
    public string Server { get; set; } = "server";
    
    [JsonPropertyName("host")]
    public string Host { get; set; } = "";
    
    [JsonPropertyName("port")]
    public int Port { get; set; } = 0;
    
    [JsonPropertyName("datamode")]
    public string DataMode { get; set; } = "stream"; // stream, single
    
    [JsonPropertyName("datatype")]
    public string DataType { get; set; } = "buffer"; // buffer, utf8, base64
    
    [JsonPropertyName("newline")]
    public string? Newline { get; set; }
}

/// <summary>
/// TCP In node - Receives TCP connections
/// SOURCE: packages/node_modules/@node-red/nodes/core/network/31-tcpin.js
/// </summary>
public class TcpInNode : BaseNode
{
    private TcpListener? _listener;
    private readonly CancellationTokenSource _cts = new();
    
    public TcpInNode(NodeConfig config) : base(config)
    {
        var tcpConfig = config as TcpInNodeConfig ?? new TcpInNodeConfig();
        
        var isServer = tcpConfig.Server == "server";
        var host = tcpConfig.Host;
        var port = tcpConfig.Port;
        var dataType = tcpConfig.DataType ?? "buffer";
        
        if (isServer && port > 0)
        {
            // Start TCP server
            Task.Run(async () =>
            {
                try
                {
                    _listener = new TcpListener(IPAddress.Any, port);
                    _listener.Start();
                    
                    Status("green", "dot", $"Listening on port {port}");
                    
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                        // Handle client in background with proper exception handling
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await HandleClientAsync(client, dataType);
                            }
                            catch (Exception ex)
                            {
                                Error($"TCP client handler error: {ex.Message}");
                            }
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown
                }
                catch (Exception ex)
                {
                    Error($"TCP server error: {ex.Message}");
                    Status("red", "ring", "Error");
                }
            });
        }
        else if (!isServer && !string.IsNullOrEmpty(host) && port > 0)
        {
            // Connect as client
            Task.Run(async () =>
            {
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync(host, port);
                    
                    Status("green", "dot", "Connected");
                    
                    await HandleClientAsync(client, dataType);
                }
                catch (Exception ex)
                {
                    Error($"TCP client error: {ex.Message}");
                    Status("red", "ring", "Error");
                }
            });
        }
    }
    
    private async Task HandleClientAsync(TcpClient client, string dataType)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var buffer = new byte[4096];
                int bytesRead;
                
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token)) > 0)
                {
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    
                    var msg = new FlowMessage();
                    
                    if (dataType == "utf8")
                    {
                        msg.Payload = Encoding.UTF8.GetString(data);
                    }
                    else if (dataType == "base64")
                    {
                        msg.Payload = Convert.ToBase64String(data);
                    }
                    else
                    {
                        msg.Payload = data;
                    }
                    
                    await ReceiveAsync(msg);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }
    
    public override async Task CloseAsync(bool removed = false)
    {
        _cts.Cancel();
        _listener?.Stop();
        
        await base.CloseAsync(removed);
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("tcp in", config => new TcpInNode(config));
    }
}
#endregion

#region UDP Node
/// <summary>
/// UDP Out node configuration
/// SOURCE: 32-udp.js
/// </summary>
public class UdpOutNodeConfig : NodeConfig
{
    [JsonPropertyName("addr")]
    public string Addr { get; set; } = "";
    
    [JsonPropertyName("iface")]
    public string? Iface { get; set; }
    
    [JsonPropertyName("port")]
    public int Port { get; set; } = 0;
    
    [JsonPropertyName("ipv")]
    public string Ipv { get; set; } = "udp4";
    
    [JsonPropertyName("outport")]
    public int OutPort { get; set; } = 0;
    
    [JsonPropertyName("base64")]
    public bool Base64 { get; set; }
    
    [JsonPropertyName("multicast")]
    public string? Multicast { get; set; }
}

/// <summary>
/// UDP Out node - Sends UDP datagrams
/// SOURCE: packages/node_modules/@node-red/nodes/core/network/32-udp.js
/// </summary>
public class UdpOutNode : BaseNode
{
    private UdpClient? _client;
    private readonly string _addr;
    private readonly int _port;
    private readonly bool _base64;
    
    public UdpOutNode(NodeConfig config) : base(config)
    {
        var udpConfig = config as UdpOutNodeConfig ?? new UdpOutNodeConfig();
        
        _addr = udpConfig.Addr ?? "";
        _port = udpConfig.Port;
        _base64 = udpConfig.Base64;
        
        if (udpConfig.OutPort > 0)
        {
            _client = new UdpClient(udpConfig.OutPort);
        }
        else
        {
            _client = new UdpClient();
        }
        
        OnInput(async (msg, send, done) =>
        {
            try
            {
                // Get address and port from config or message
                var addr = string.IsNullOrEmpty(_addr) 
                    ? msg.AdditionalProperties?.TryGetValue("ip", out var ip) == true ? ip.ToString() : ""
                    : _addr;
                
                var port = _port > 0 ? _port
                    : msg.AdditionalProperties?.TryGetValue("port", out var p) == true ? p.GetInt32() : 0;
                
                if (string.IsNullOrEmpty(addr) || port <= 0)
                {
                    done(new Exception("No address or port specified"));
                    return;
                }
                
                // Get data
                byte[] data;
                if (msg.Payload is byte[] bytes)
                {
                    data = bytes;
                }
                else if (_base64 && msg.Payload is string str)
                {
                    data = Convert.FromBase64String(str);
                }
                else
                {
                    data = Encoding.UTF8.GetBytes(msg.Payload?.ToString() ?? "");
                }
                
                // Send datagram
                await _client!.SendAsync(data, data.Length, addr, port);
                
                send(msg);
                done(null);
            }
            catch (Exception ex)
            {
                done(ex);
            }
        });
    }
    
    public override async Task CloseAsync(bool removed = false)
    {
        _client?.Close();
        _client?.Dispose();
        
        await base.CloseAsync(removed);
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("udp out", config => new UdpOutNode(config));
    }
}
#endregion

#region WebSocket Node
/// <summary>
/// WebSocket In node configuration
/// SOURCE: 22-websocket.js
/// </summary>
public class WebSocketInNodeConfig : NodeConfig
{
    [JsonPropertyName("server")]
    public string Server { get; set; } = "";
    
    [JsonPropertyName("client")]
    public string Client { get; set; } = "";
}

/// <summary>
/// WebSocket In node - Receives WebSocket messages
/// SOURCE: packages/node_modules/@node-red/nodes/core/network/22-websocket.js
/// </summary>
public class WebSocketInNode : BaseNode
{
    private ClientWebSocket? _client;
    private readonly CancellationTokenSource _cts = new();
    
    public WebSocketInNode(NodeConfig config) : base(config)
    {
        var wsConfig = config as WebSocketInNodeConfig ?? new WebSocketInNodeConfig();
        
        var clientUrl = wsConfig.Client;
        
        if (!string.IsNullOrEmpty(clientUrl))
        {
            // Connect as WebSocket client
            Task.Run(async () =>
            {
                try
                {
                    _client = new ClientWebSocket();
                    await _client.ConnectAsync(new Uri(clientUrl), _cts.Token);
                    
                    Status("green", "dot", "Connected");
                    
                    var buffer = new byte[8192];
                    
                    while (_client.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                    {
                        var result = await _client.ReceiveAsync(buffer, _cts.Token);
                        
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }
                        
                        var data = new byte[result.Count];
                        Array.Copy(buffer, data, result.Count);
                        
                        var msg = new FlowMessage();
                        
                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            msg.Payload = Encoding.UTF8.GetString(data);
                        }
                        else
                        {
                            msg.Payload = data;
                        }
                        
                        await ReceiveAsync(msg);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown
                }
                catch (Exception ex)
                {
                    Error($"WebSocket error: {ex.Message}");
                    Status("red", "ring", "Error");
                }
            });
        }
    }
    
    public override async Task CloseAsync(bool removed = false)
    {
        _cts.Cancel();
        
        if (_client?.State == WebSocketState.Open)
        {
            try
            {
                await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Node closing", CancellationToken.None);
            }
            catch { }
        }
        
        _client?.Dispose();
        
        await base.CloseAsync(removed);
    }
    
    public static void Register()
    {
        NodeTypeRegistry.RegisterType("websocket in", config => new WebSocketInNode(config));
    }
}
#endregion
