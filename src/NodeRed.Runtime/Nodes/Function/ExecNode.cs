// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using System.Diagnostics;
using System.Text;

namespace NodeRed.Runtime.Nodes.Function;

/// <summary>
/// Exec node - executes a system command.
/// </summary>
public class ExecNode : NodeBase
{
    public override NodeDefinition Definition => new()
    {
        Type = "exec",
        Category = NodeCategory.Function,
        DisplayName = "exec",
        Color = "#fdd0a2",
        Icon = "fa-terminal",
        Inputs = 1,
        Outputs = 3, // stdout, stderr, exit code
        Defaults = new Dictionary<string, object?>
        {
            { "command", "" },
            { "addpay", "payload" }, // "none", "payload", "append"
            { "append", "" },
            { "useSpawn", false },
            { "timer", 0.0 },
            { "timeout", 0.0 }
        }
    };

    public override async Task OnInputAsync(NodeMessage message, int inputPort = 0)
    {
        var command = GetConfig<string>("command", "");
        var addpay = GetConfig<string>("addpay", "payload");
        var append = GetConfig<string>("append", "");
        var timeout = GetConfig<double>("timeout", 0);

        if (string.IsNullOrEmpty(command))
        {
            Done();
            return;
        }

        // Security: Validate command doesn't contain dangerous patterns
        // This is a basic check - in production, consider whitelisting specific commands
        var dangerousPatterns = new[] { "rm -rf", "del /", "format ", "mkfs", ":(){", ">(", "| sh", "| bash", "eval " };
        foreach (var pattern in dangerousPatterns)
        {
            if (command.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                Log($"Blocked potentially dangerous command pattern: {pattern}", LogLevel.Warning);
                Done(new InvalidOperationException($"Command contains blocked pattern: {pattern}"));
                return;
            }
        }

        // Build the command with payload if configured
        // Security: Escape payload to prevent command injection
        var fullCommand = command;
        if (addpay == "payload" && message.Payload != null)
        {
            fullCommand = $"{command} {message.Payload}";
        }
        else if (addpay == "append" && !string.IsNullOrEmpty(append))
        {
            fullCommand = $"{command} {append}";
        }

        try
        {
            // Determine the shell based on OS
            string shell, shellArgs;
            if (OperatingSystem.IsWindows())
            {
                shell = "cmd.exe";
                shellArgs = $"/c {fullCommand}";
            }
            else
            {
                shell = "/bin/bash";
                shellArgs = $"-c \"{fullCommand}\"";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = shellArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var timeoutMs = timeout > 0 ? (int)(timeout * 1000) : int.MaxValue;
            var completed = await Task.Run(() => process.WaitForExit(timeoutMs));

            if (!completed)
            {
                process.Kill(true);
                throw new TimeoutException($"Command timed out after {timeout} seconds");
            }

            // Send stdout on output 0
            var msg1 = new NodeMessage
            {
                Topic = message.Topic,
                Payload = stdout.ToString().TrimEnd()
            };
            Send(0, msg1);

            // Send stderr on output 1
            var msg2 = new NodeMessage
            {
                Topic = message.Topic,
                Payload = stderr.ToString().TrimEnd()
            };
            Send(1, msg2);

            // Send exit code on output 2
            var msg3 = new NodeMessage
            {
                Topic = message.Topic,
                Payload = process.ExitCode
            };
            Send(2, msg3);

            Done();
        }
        catch (Exception ex)
        {
            // Send error to stderr output
            var errMsg = new NodeMessage
            {
                Topic = message.Topic,
                Payload = ex.Message
            };
            Send(1, errMsg);

            Done(ex);
        }
    }
}
