// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

namespace NodeRed.Blazor.Models;

/// <summary>
/// Defines the schema for a node property field
/// </summary>
public class NodePropertyField
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string Icon { get; set; } = "";
    public PropertyFieldType Type { get; set; } = PropertyFieldType.Text;
    public string DefaultValue { get; set; } = "";
    public string Placeholder { get; set; } = "";
    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "";
    public List<PropertyOption>? Options { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Step { get; set; }
    public int? Rows { get; set; }
    public string? ShowWhen { get; set; } // Condition: "fieldName=value" to show this field
    public string? HideWhen { get; set; } // Condition: "fieldName=value" to hide this field
    public bool IsSmall { get; set; } = false;
    public bool IsFullWidth { get; set; } = false;
}

public class PropertyOption
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
}

public enum PropertyFieldType
{
    Text,
    Number,
    Select,
    Checkbox,
    TextArea,
    Code,
    Info,
    Button
}

/// <summary>
/// Registry of node property schemas
/// </summary>
public static class NodePropertySchemas
{
    private static readonly Dictionary<string, List<NodePropertyField>> _schemas = new()
    {
        ["inject"] = new List<NodePropertyField>
        {
            new() { Name = "payloadType", Label = "msg.payload", Icon = "fa fa-envelope", Type = PropertyFieldType.Select, DefaultValue = "date",
                Options = new List<PropertyOption>
                {
                    new() { Value = "date", Label = "timestamp" },
                    new() { Value = "str", Label = "string" },
                    new() { Value = "num", Label = "number" },
                    new() { Value = "bool", Label = "boolean" },
                    new() { Value = "json", Label = "JSON" }
                }
            },
            new() { Name = "payloadValue", Label = "", Type = PropertyFieldType.Text, Placeholder = "value", HideWhen = "payloadType=date" },
            new() { Name = "topic", Label = "msg.topic", Icon = "fa fa-tasks", Type = PropertyFieldType.Text },
            new() { Name = "repeatType", Label = "Repeat", Icon = "fa fa-repeat", Type = PropertyFieldType.Select, DefaultValue = "none",
                Options = new List<PropertyOption>
                {
                    new() { Value = "none", Label = "none" },
                    new() { Value = "interval", Label = "interval" }
                }
            },
            new() { Name = "repeatInterval", Label = "", Type = PropertyFieldType.Number, DefaultValue = "1", Prefix = "every", Suffix = "seconds", ShowWhen = "repeatType=interval", IsSmall = true }
        },

        ["debug"] = new List<NodePropertyField>
        {
            new() { Name = "output", Label = "Output", Icon = "fa fa-list", Type = PropertyFieldType.Select, DefaultValue = "payload",
                Options = new List<PropertyOption>
                {
                    new() { Value = "payload", Label = "msg.payload" },
                    new() { Value = "true", Label = "complete msg object" }
                }
            },
            new() { Name = "toSidebar", Label = "To", Type = PropertyFieldType.Checkbox, DefaultValue = "true", Suffix = "debug window" },
            new() { Name = "toConsole", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "false", Suffix = "system console" },
            new() { Name = "toStatus", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "false", Suffix = "node status" }
        },

        ["function"] = new List<NodePropertyField>
        {
            new() { Name = "outputs", Label = "Outputs", Icon = "fa fa-random", Type = PropertyFieldType.Number, DefaultValue = "1", Min = 1, Max = 10, IsSmall = true },
            new() { Name = "func", Label = "Function", Type = PropertyFieldType.Code, DefaultValue = "return msg;", Rows = 10, IsFullWidth = true }
        },

        ["change"] = new List<NodePropertyField>
        {
            new() { Name = "action", Label = "Rules", Type = PropertyFieldType.Select, DefaultValue = "set",
                Options = new List<PropertyOption>
                {
                    new() { Value = "set", Label = "Set" },
                    new() { Value = "change", Label = "Change" },
                    new() { Value = "delete", Label = "Delete" },
                    new() { Value = "move", Label = "Move" }
                }
            },
            new() { Name = "property", Label = "", Type = PropertyFieldType.Text, Prefix = "msg.", IsSmall = true },
            new() { Name = "value", Label = "", Type = PropertyFieldType.Text, Prefix = "to", ShowWhen = "action=set" }
        },

        ["switch"] = new List<NodePropertyField>
        {
            new() { Name = "property", Label = "Property", Type = PropertyFieldType.Text, Prefix = "msg.", DefaultValue = "payload" }
        },

        ["delay"] = new List<NodePropertyField>
        {
            new() { Name = "action", Label = "Action", Type = PropertyFieldType.Select, DefaultValue = "delay",
                Options = new List<PropertyOption>
                {
                    new() { Value = "delay", Label = "Delay each message" },
                    new() { Value = "rate", Label = "Rate Limit Messages" }
                }
            },
            new() { Name = "timeout", Label = "For", Type = PropertyFieldType.Number, DefaultValue = "1", Min = 0, IsSmall = true },
            new() { Name = "timeoutUnits", Label = "", Type = PropertyFieldType.Select, DefaultValue = "s",
                Options = new List<PropertyOption>
                {
                    new() { Value = "ms", Label = "Milliseconds" },
                    new() { Value = "s", Label = "Seconds" },
                    new() { Value = "m", Label = "Minutes" }
                }
            }
        },

        ["template"] = new List<NodePropertyField>
        {
            new() { Name = "property", Label = "Property", Type = PropertyFieldType.Text, Prefix = "msg.", DefaultValue = "payload", IsSmall = true },
            new() { Name = "template", Label = "Template", Type = PropertyFieldType.TextArea, DefaultValue = "This is the payload: {{payload}}", Rows = 8, IsFullWidth = true },
            new() { Name = "outputAs", Label = "Output as", Type = PropertyFieldType.Select, DefaultValue = "str",
                Options = new List<PropertyOption>
                {
                    new() { Value = "str", Label = "Plain text" },
                    new() { Value = "json", Label = "Parsed JSON" }
                }
            }
        },

        ["range"] = new List<NodePropertyField>
        {
            new() { Name = "action", Label = "Action", Type = PropertyFieldType.Select, DefaultValue = "scale",
                Options = new List<PropertyOption>
                {
                    new() { Value = "scale", Label = "Scale" },
                    new() { Value = "clamp", Label = "Clamp" },
                    new() { Value = "roll", Label = "Wrap" }
                }
            },
            new() { Name = "inMin", Label = "Input Range", Type = PropertyFieldType.Number, DefaultValue = "0", IsSmall = true },
            new() { Name = "inMax", Label = "", Type = PropertyFieldType.Number, DefaultValue = "100", Prefix = "to", IsSmall = true },
            new() { Name = "outMin", Label = "Output Range", Type = PropertyFieldType.Number, DefaultValue = "0", IsSmall = true },
            new() { Name = "outMax", Label = "", Type = PropertyFieldType.Number, DefaultValue = "1", Prefix = "to", IsSmall = true }
        },

        ["trigger"] = new List<NodePropertyField>
        {
            new() { Name = "sendType", Label = "Send", Type = PropertyFieldType.Select, DefaultValue = "str",
                Options = new List<PropertyOption>
                {
                    new() { Value = "str", Label = "string" },
                    new() { Value = "num", Label = "number" },
                    new() { Value = "pay", Label = "the original msg.payload" }
                }
            },
            new() { Name = "sendValue", Label = "", Type = PropertyFieldType.Text, HideWhen = "sendType=pay" },
            new() { Name = "duration", Label = "then wait for", Type = PropertyFieldType.Number, DefaultValue = "250", Min = 0, IsSmall = true },
            new() { Name = "durationUnits", Label = "", Type = PropertyFieldType.Select, DefaultValue = "ms",
                Options = new List<PropertyOption>
                {
                    new() { Value = "ms", Label = "milliseconds" },
                    new() { Value = "s", Label = "seconds" },
                    new() { Value = "m", Label = "minutes" }
                }
            },
            new() { Name = "thenType", Label = "then send", Type = PropertyFieldType.Select, DefaultValue = "str",
                Options = new List<PropertyOption>
                {
                    new() { Value = "str", Label = "string" },
                    new() { Value = "num", Label = "number" },
                    new() { Value = "nul", Label = "nothing" }
                }
            },
            new() { Name = "thenValue", Label = "", Type = PropertyFieldType.Text, HideWhen = "thenType=nul" }
        },

        ["exec"] = new List<NodePropertyField>
        {
            new() { Name = "command", Label = "Command", Icon = "fa fa-terminal", Type = PropertyFieldType.Text, Placeholder = "command" },
            new() { Name = "append", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "true", Suffix = "Append msg.payload to command" },
            new() { Name = "useSpawn", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "false", Suffix = "Use spawn() instead of exec()" },
            new() { Name = "timeout", Label = "Timeout", Type = PropertyFieldType.Number, DefaultValue = "0", Min = 0, Suffix = "seconds (0 = no timeout)", IsSmall = true }
        },

        ["rbe"] = new List<NodePropertyField>
        {
            new() { Name = "func", Label = "Mode", Type = PropertyFieldType.Select, DefaultValue = "rbe",
                Options = new List<PropertyOption>
                {
                    new() { Value = "rbe", Label = "Block unless value changes" },
                    new() { Value = "rbei", Label = "Block unless value changes (ignore initial)" },
                    new() { Value = "deadband", Label = "Block unless value changes by more than" }
                }
            },
            new() { Name = "percent", Label = "Value", Type = PropertyFieldType.Number, DefaultValue = "0", Suffix = "%", ShowWhen = "func=deadband", IsSmall = true },
            new() { Name = "property", Label = "Property", Type = PropertyFieldType.Text, Prefix = "msg.", DefaultValue = "payload" }
        },

        ["split"] = new List<NodePropertyField>
        {
            new() { Name = "separator", Label = "Split on", Type = PropertyFieldType.Text, DefaultValue = "\\n", Placeholder = "\\n" },
            new() { Name = "stream", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "false", Suffix = "Handle as a stream of messages" }
        },

        ["join"] = new List<NodePropertyField>
        {
            new() { Name = "mode", Label = "Mode", Type = PropertyFieldType.Select, DefaultValue = "auto",
                Options = new List<PropertyOption>
                {
                    new() { Value = "auto", Label = "automatic" },
                    new() { Value = "manual", Label = "manual" }
                }
            },
            new() { Name = "separator", Label = "using", Type = PropertyFieldType.Text, Placeholder = "separator", ShowWhen = "mode=manual" },
            new() { Name = "count", Label = "After", Type = PropertyFieldType.Number, DefaultValue = "0", Min = 1, Suffix = "message parts", ShowWhen = "mode=manual", IsSmall = true }
        },

        ["sort"] = new List<NodePropertyField>
        {
            new() { Name = "property", Label = "Target", Type = PropertyFieldType.Text, Prefix = "msg.", DefaultValue = "payload" },
            new() { Name = "order", Label = "Order", Type = PropertyFieldType.Select, DefaultValue = "asc",
                Options = new List<PropertyOption>
                {
                    new() { Value = "asc", Label = "ascending" },
                    new() { Value = "desc", Label = "descending" }
                }
            },
            new() { Name = "asNumber", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "false", Suffix = "Compare as numbers" }
        },

        ["batch"] = new List<NodePropertyField>
        {
            new() { Name = "mode", Label = "Mode", Type = PropertyFieldType.Select, DefaultValue = "count",
                Options = new List<PropertyOption>
                {
                    new() { Value = "count", Label = "Number of messages" },
                    new() { Value = "interval", Label = "Time interval" },
                    new() { Value = "concat", Label = "Concatenate sequences" }
                }
            },
            new() { Name = "count", Label = "Number", Type = PropertyFieldType.Number, DefaultValue = "10", Min = 1, ShowWhen = "mode=count", IsSmall = true },
            new() { Name = "overlap", Label = "Overlap", Type = PropertyFieldType.Number, DefaultValue = "0", Min = 0, ShowWhen = "mode=count", IsSmall = true }
        },

        ["csv"] = new List<NodePropertyField>
        {
            new() { Name = "columns", Label = "Columns", Type = PropertyFieldType.Text, Placeholder = "a,b,c (leave blank for auto)" },
            new() { Name = "separator", Label = "Separator", Type = PropertyFieldType.Text, DefaultValue = ",", IsSmall = true },
            new() { Name = "headers", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "true", Suffix = "First row contains column names" },
            new() { Name = "skipEmpty", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "false", Suffix = "Skip empty lines" }
        },

        ["json"] = new List<NodePropertyField>
        {
            new() { Name = "action", Label = "Action", Type = PropertyFieldType.Select, DefaultValue = "obj",
                Options = new List<PropertyOption>
                {
                    new() { Value = "obj", Label = "Always convert to JavaScript Object" },
                    new() { Value = "str", Label = "Always convert to JSON String" }
                }
            },
            new() { Name = "property", Label = "Property", Type = PropertyFieldType.Text, Prefix = "msg.", DefaultValue = "payload" }
        },

        ["xml"] = new List<NodePropertyField>
        {
            new() { Name = "property", Label = "Property", Type = PropertyFieldType.Text, Prefix = "msg.", DefaultValue = "payload" },
            new() { Name = "attrPrefix", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "true", Suffix = "Keep attribute prefixes" }
        },

        ["html"] = new List<NodePropertyField>
        {
            new() { Name = "selector", Label = "Selector", Type = PropertyFieldType.Text, Placeholder = "CSS selector (e.g., .class, #id, tag)" },
            new() { Name = "property", Label = "Property", Type = PropertyFieldType.Text, Prefix = "msg.", DefaultValue = "payload" }
        },

        ["yaml"] = new List<NodePropertyField>
        {
            new() { Name = "property", Label = "Property", Type = PropertyFieldType.Text, Prefix = "msg.", DefaultValue = "payload" }
        },

        ["file"] = new List<NodePropertyField>
        {
            new() { Name = "filename", Label = "Filename", Icon = "fa fa-file", Type = PropertyFieldType.Text, Placeholder = "Path to file" },
            new() { Name = "action", Label = "Action", Type = PropertyFieldType.Select, DefaultValue = "append",
                Options = new List<PropertyOption>
                {
                    new() { Value = "append", Label = "Append to file" },
                    new() { Value = "overwrite", Label = "Overwrite file" },
                    new() { Value = "delete", Label = "Delete file" }
                }
            },
            new() { Name = "addNewline", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "true", Suffix = "Add newline after each payload" },
            new() { Name = "createDir", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "false", Suffix = "Create directory if it doesn't exist" },
            new() { Name = "encoding", Label = "Encoding", Type = PropertyFieldType.Select, DefaultValue = "utf8",
                Options = new List<PropertyOption>
                {
                    new() { Value = "utf8", Label = "UTF-8" },
                    new() { Value = "ascii", Label = "ASCII" },
                    new() { Value = "binary", Label = "Binary" }
                }
            }
        },

        ["file in"] = new List<NodePropertyField>
        {
            new() { Name = "filename", Label = "Filename", Icon = "fa fa-file", Type = PropertyFieldType.Text, Placeholder = "Path to file" },
            new() { Name = "format", Label = "Output", Type = PropertyFieldType.Select, DefaultValue = "utf8",
                Options = new List<PropertyOption>
                {
                    new() { Value = "utf8", Label = "a UTF-8 string" },
                    new() { Value = "lines", Label = "a single message per line" },
                    new() { Value = "stream", Label = "a stream of Buffer chunks" },
                    new() { Value = "binary", Label = "a single Buffer object" }
                }
            }
        },

        ["watch"] = new List<NodePropertyField>
        {
            new() { Name = "files", Label = "Files", Icon = "fa fa-eye", Type = PropertyFieldType.Text, Placeholder = "comma-separated list of files/directories" },
            new() { Name = "recursive", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "true", Suffix = "Watch subdirectories recursively" }
        },

        ["http in"] = new List<NodePropertyField>
        {
            new() { Name = "method", Label = "Method", Type = PropertyFieldType.Select, DefaultValue = "get",
                Options = new List<PropertyOption>
                {
                    new() { Value = "get", Label = "GET" },
                    new() { Value = "post", Label = "POST" },
                    new() { Value = "put", Label = "PUT" },
                    new() { Value = "delete", Label = "DELETE" },
                    new() { Value = "patch", Label = "PATCH" }
                }
            },
            new() { Name = "url", Label = "URL", Type = PropertyFieldType.Text, DefaultValue = "/", Placeholder = "/url" }
        },

        ["http response"] = new List<NodePropertyField>
        {
            new() { Name = "info", Label = "", Type = PropertyFieldType.Info, DefaultValue = "This node sends responses to requests received from an HTTP Input node." }
        },

        ["http request"] = new List<NodePropertyField>
        {
            new() { Name = "method", Label = "Method", Type = PropertyFieldType.Select, DefaultValue = "GET",
                Options = new List<PropertyOption>
                {
                    new() { Value = "GET", Label = "GET" },
                    new() { Value = "POST", Label = "POST" },
                    new() { Value = "PUT", Label = "PUT" },
                    new() { Value = "DELETE", Label = "DELETE" },
                    new() { Value = "PATCH", Label = "PATCH" },
                    new() { Value = "HEAD", Label = "HEAD" },
                    new() { Value = "OPTIONS", Label = "OPTIONS" }
                }
            },
            new() { Name = "url", Label = "URL", Type = PropertyFieldType.Text, Placeholder = "https://..." },
            new() { Name = "ret", Label = "Return", Type = PropertyFieldType.Select, DefaultValue = "txt",
                Options = new List<PropertyOption>
                {
                    new() { Value = "txt", Label = "a UTF-8 string" },
                    new() { Value = "bin", Label = "a binary buffer" },
                    new() { Value = "obj", Label = "a parsed JSON object" }
                }
            }
        },

        ["mqtt in"] = new List<NodePropertyField>
        {
            new() { Name = "topic", Label = "Topic", Type = PropertyFieldType.Text, Placeholder = "topic" },
            new() { Name = "qos", Label = "QoS", Type = PropertyFieldType.Select, DefaultValue = "0",
                Options = new List<PropertyOption>
                {
                    new() { Value = "0", Label = "0" },
                    new() { Value = "1", Label = "1" },
                    new() { Value = "2", Label = "2" }
                }
            },
            new() { Name = "broker", Label = "Server", Type = PropertyFieldType.Text, DefaultValue = "localhost", Placeholder = "localhost" },
            new() { Name = "port", Label = "Port", Type = PropertyFieldType.Number, DefaultValue = "1883", Min = 1, Max = 65535 }
        },

        ["mqtt out"] = new List<NodePropertyField>
        {
            new() { Name = "topic", Label = "Topic", Type = PropertyFieldType.Text, Placeholder = "topic" },
            new() { Name = "qos", Label = "QoS", Type = PropertyFieldType.Select, DefaultValue = "0",
                Options = new List<PropertyOption>
                {
                    new() { Value = "0", Label = "0" },
                    new() { Value = "1", Label = "1" },
                    new() { Value = "2", Label = "2" }
                }
            },
            new() { Name = "broker", Label = "Server", Type = PropertyFieldType.Text, DefaultValue = "localhost", Placeholder = "localhost" },
            new() { Name = "port", Label = "Port", Type = PropertyFieldType.Number, DefaultValue = "1883", Min = 1, Max = 65535 }
        },

        ["websocket in"] = new List<NodePropertyField>
        {
            new() { Name = "type", Label = "Type", Type = PropertyFieldType.Select, DefaultValue = "server",
                Options = new List<PropertyOption>
                {
                    new() { Value = "server", Label = "Listen on" },
                    new() { Value = "client", Label = "Connect to" }
                }
            },
            new() { Name = "path", Label = "Path", Type = PropertyFieldType.Text, DefaultValue = "/ws", Placeholder = "/ws" }
        },

        ["websocket out"] = new List<NodePropertyField>
        {
            new() { Name = "type", Label = "Type", Type = PropertyFieldType.Select, DefaultValue = "server",
                Options = new List<PropertyOption>
                {
                    new() { Value = "server", Label = "Listen on" },
                    new() { Value = "client", Label = "Connect to" }
                }
            },
            new() { Name = "path", Label = "Path", Type = PropertyFieldType.Text, DefaultValue = "/ws", Placeholder = "/ws" }
        },

        ["tcp in"] = new List<NodePropertyField>
        {
            new() { Name = "type", Label = "Type", Type = PropertyFieldType.Select, DefaultValue = "server",
                Options = new List<PropertyOption>
                {
                    new() { Value = "server", Label = "Listen on port" },
                    new() { Value = "client", Label = "Connect to" }
                }
            },
            new() { Name = "host", Label = "Host", Type = PropertyFieldType.Text, DefaultValue = "localhost", Placeholder = "localhost" },
            new() { Name = "port", Label = "Port", Type = PropertyFieldType.Number, DefaultValue = "12345", Min = 1, Max = 65535 }
        },

        ["tcp out"] = new List<NodePropertyField>
        {
            new() { Name = "type", Label = "Type", Type = PropertyFieldType.Select, DefaultValue = "client",
                Options = new List<PropertyOption>
                {
                    new() { Value = "server", Label = "Reply to TCP" },
                    new() { Value = "client", Label = "Connect to" }
                }
            },
            new() { Name = "host", Label = "Host", Type = PropertyFieldType.Text, DefaultValue = "localhost", Placeholder = "localhost" },
            new() { Name = "port", Label = "Port", Type = PropertyFieldType.Number, DefaultValue = "12345", Min = 1, Max = 65535 }
        },

        ["udp in"] = new List<NodePropertyField>
        {
            new() { Name = "address", Label = "Address", Type = PropertyFieldType.Text, Placeholder = "127.0.0.1" },
            new() { Name = "port", Label = "Port", Type = PropertyFieldType.Number, DefaultValue = "12345", Min = 1, Max = 65535 },
            new() { Name = "multicast", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "false", Suffix = "Multicast" }
        },

        ["udp out"] = new List<NodePropertyField>
        {
            new() { Name = "address", Label = "Address", Type = PropertyFieldType.Text, Placeholder = "127.0.0.1" },
            new() { Name = "port", Label = "Port", Type = PropertyFieldType.Number, DefaultValue = "12345", Min = 1, Max = 65535 },
            new() { Name = "multicast", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "false", Suffix = "Multicast" }
        },

        ["link in"] = new List<NodePropertyField>
        {
            new() { Name = "linkName", Label = "Link Name", Type = PropertyFieldType.Text, Placeholder = "link name" }
        },

        ["link out"] = new List<NodePropertyField>
        {
            new() { Name = "linkName", Label = "Link Name", Type = PropertyFieldType.Text, Placeholder = "link name" }
        },

        ["link call"] = new List<NodePropertyField>
        {
            new() { Name = "linkName", Label = "Link Name", Type = PropertyFieldType.Text, Placeholder = "link name" }
        },

        ["complete"] = new List<NodePropertyField>
        {
            new() { Name = "scope", Label = "Scope", Type = PropertyFieldType.Select, DefaultValue = "all",
                Options = new List<PropertyOption>
                {
                    new() { Value = "all", Label = "All nodes" },
                    new() { Value = "selected", Label = "Selected nodes" }
                }
            },
            new() { Name = "info", Label = "", Type = PropertyFieldType.Info, DefaultValue = "Triggers when a node completes handling a message." }
        },

        ["status"] = new List<NodePropertyField>
        {
            new() { Name = "scope", Label = "Scope", Type = PropertyFieldType.Select, DefaultValue = "all",
                Options = new List<PropertyOption>
                {
                    new() { Value = "all", Label = "All nodes" },
                    new() { Value = "selected", Label = "Selected nodes" }
                }
            },
            new() { Name = "info", Label = "", Type = PropertyFieldType.Info, DefaultValue = "Triggers when a node changes its status." }
        },

        ["catch"] = new List<NodePropertyField>
        {
            new() { Name = "scope", Label = "Scope", Type = PropertyFieldType.Select, DefaultValue = "all",
                Options = new List<PropertyOption>
                {
                    new() { Value = "all", Label = "All nodes" },
                    new() { Value = "selected", Label = "Selected nodes" }
                }
            },
            new() { Name = "uncaught", Label = "", Type = PropertyFieldType.Checkbox, DefaultValue = "false", Suffix = "Catch errors not caught by other catch nodes" }
        },

        ["comment"] = new List<NodePropertyField>
        {
            new() { Name = "text", Label = "Comment", Type = PropertyFieldType.TextArea, Placeholder = "Enter comment text...", Rows = 5, IsFullWidth = true }
        }
    };

    public static List<NodePropertyField> GetSchema(string nodeType)
    {
        return _schemas.TryGetValue(nodeType, out var schema) ? schema : new List<NodePropertyField>();
    }

    public static bool HasSchema(string nodeType)
    {
        return _schemas.ContainsKey(nodeType);
    }
}
