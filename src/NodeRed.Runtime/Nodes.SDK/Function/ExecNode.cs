// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;
using NodeRed.Core.Enums;
using NodeRed.SDK;
using System.Diagnostics;
using System.Text;
using SdkNodeBase = NodeRed.SDK.NodeBase;

namespace NodeRed.Runtime.Nodes.SDK.Function;

/// <summary>
/// Exec node - executes a system command.
/// </summary>
[NodeType("exec", "exec",
    Category = NodeCategory.Function,
    Color = "#e6e0f8",
    Icon = "fa fa-terminal",
    Inputs = 1,
    Outputs = 3)]
public class ExecNode : SdkNodeBase
{
    protected override List<NodePropertyDefinition> DefineProperties() =>
        PropertyBuilder.Create()
            .AddText("name", "Name", icon: "fa fa-tag")
            .AddText("command", "Command", icon: "fa fa-terminal", required: true)
            .AddSelect("addpay", "Append", new[]
            {
                ("none", "Nothing"),
                ("payload", "msg.payload"),
                ("append", "Configured string")
            }, defaultValue: "payload")
            .AddText("append", "String to append", showWhen: "addpay=append")
            .AddCheckbox("useSpawn", "Use spawn() instead of exec()", defaultValue: false)
            .AddNumber("timeout", "Timeout", suffix: "seconds", defaultValue: 0, min: 0)
            .Build();

    protected override Dictionary<string, object?> DefineDefaults() => new()
    {
        { "name", "" },
        { "command", "" },
        { "addpay", "payload" },
        { "append", "" },
        { "useSpawn", false },
        { "timeout", 0.0 }
    };

    protected override NodeHelpText DefineHelp() => HelpBuilder.Create()
        .Summary("Executes a system command and returns the results.")
        .AddInput("msg.payload", "string", "Optional data to append to the command")
        .AddOutput("stdout", "string", "Standard output from the command (output 1)")
        .AddOutput("stderr", "string", "Standard error from the command (output 2)")
        .AddOutput("code", "number", "Exit code from the command (output 3)")
        .Details(@"
The Exec node runs a system command and provides three outputs:

1. **stdout** - The standard output from the command
2. **stderr** - The standard error from the command
3. **exit code** - The numeric exit code (0 typically means success)

**Options:**
- **Append** - Add msg.payload or a configured string to the command
- **Use spawn** - Use spawn() for long-running processes
- **Timeout** - Kill the process if it runs longer than this

**Security Note:** Be careful with commands that include user input.
Avoid passing untrusted data directly to shell commands.")
        .Build();

    protected override async Task OnInputAsync(NodeMessage msg, SendDelegate send, DoneDelegate done)
    {
        var command = GetConfig<string>("command", "");
        var addpay = GetConfig<string>("addpay", "payload");
        var append = GetConfig<string>("append", "");
        var timeout = GetConfig<double>("timeout", 0);

        if (string.IsNullOrEmpty(command))
        {
            done();
            return;
        }

        // Security: Validate command doesn't contain dangerous patterns
        var dangerousPatterns = new[] { "rm -rf", "del /", "format ", "mkfs", ":(){", ">(", "| sh", "| bash", "eval " };
        foreach (var pattern in dangerousPatterns)
        {
            if (command.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                Warn($"Blocked potentially dangerous command pattern: {pattern}");
                done(new InvalidOperationException($"Command contains blocked pattern: {pattern}"));
                return;
            }
        }

        // Build the command with payload if configured
        var fullCommand = command;
        if (addpay == "payload" && msg.Payload != null)
        {
            fullCommand = $"{command} {msg.Payload}";
        }
        else if (addpay == "append" && !string.IsNullOrEmpty(append))
        {
            fullCommand = $"{command} {append}";
        }

        try
        {
            Status("running", StatusFill.Blue, SdkStatusShape.Ring);

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

            if (timeout > 0)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill(true);
                    throw new TimeoutException($"Command timed out after {timeout} seconds");
                }
            }
            else
            {
                await process.WaitForExitAsync();
            }

            ClearStatus();

            // Send stdout on output 0
            var msg1 = NewMessage(stdout.ToString().TrimEnd(), msg.Topic);
            send(0, msg1);

            // Send stderr on output 1
            var msg2 = NewMessage(stderr.ToString().TrimEnd(), msg.Topic);
            send(1, msg2);

            // Send exit code on output 2
            var msg3 = NewMessage(process.ExitCode, msg.Topic);
            send(2, msg3);

            done();
        }
        catch (Exception ex)
        {
            Status("error", StatusFill.Red, SdkStatusShape.Dot);

            // Send error to stderr output
            var errMsg = NewMessage(ex.Message, msg.Topic);
            send(1, errMsg);

            done(ex);
        }
    }
}
