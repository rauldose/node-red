// ============================================================
// SOURCE: packages/node_modules/@node-red/editor-api/lib/editor/comms.js
// LINES: 1-200+
// ============================================================
// ORIGINAL CODE:
// ------------------------------------------------------------
// WebSocket communication for real-time updates
// var connections = [];
// function init(_server, settings, runtimeAPI) { ... }
// function start() { ... }
// function stop() { ... }
// function publish(topic, data, retain) { ... }
// ------------------------------------------------------------
// TRANSLATION:
// ------------------------------------------------------------

// Copyright JS Foundation and other contributors, http://js.foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using NodeRed.Util;

namespace NodeRed.EditorApi
{
    /// <summary>
    /// Communication message for WebSocket/SignalR.
    /// </summary>
    public class CommsMessage
    {
        public string Topic { get; set; } = string.Empty;
        public object? Data { get; set; }
    }

    /// <summary>
    /// Connection entry for a connected client.
    /// </summary>
    public class CommsConnection
    {
        public string Id { get; set; } = string.Empty;
        public Func<CommsMessage, Task>? SendAsync { get; set; }
        public AuthUser? User { get; set; }
    }

    /// <summary>
    /// WebSocket/SignalR communication handler.
    /// Provides real-time communication between the editor and runtime.
    /// </summary>
    /// <remarks>
    /// This is a translation of @node-red/editor-api/lib/editor/comms.js
    /// In Blazor, this will be implemented using SignalR instead of raw WebSockets.
    /// </remarks>
    public class CommsHandler
    {
        private Runtime.Settings? _settings;
        private Runtime.FlowsManager? _runtimeApi;
        private readonly ConcurrentDictionary<string, CommsConnection> _connections = new();
        private readonly ConcurrentDictionary<string, CommsMessage> _retainedMessages = new();
        private bool _started;

        /// <summary>
        /// Initialize the comms handler.
        /// </summary>
        public void Init(Runtime.Settings settings, Runtime.FlowsManager runtimeApi)
        {
            _settings = settings;
            _runtimeApi = runtimeApi;
        }

        /// <summary>
        /// Start the comms handler.
        /// </summary>
        public void Start()
        {
            if (_started) return;

            // Subscribe to runtime events
            Events.Instance.On("comms", HandleCommsEvent);
            Events.Instance.On("runtime-event", HandleRuntimeEvent);

            _started = true;
            Log.Info("Comms started");
        }

        /// <summary>
        /// Stop the comms handler.
        /// </summary>
        public void Stop()
        {
            if (!_started) return;

            // Note: In the original, events are unsubscribed here
            // Events.Instance.Off("comms", HandleCommsEvent);
            // Events.Instance.Off("runtime-event", HandleRuntimeEvent);

            // Close all connections
            _connections.Clear();

            _started = false;
            Log.Info("Comms stopped");
        }

        /// <summary>
        /// Add a connection.
        /// </summary>
        public void AddConnection(CommsConnection connection)
        {
            _connections[connection.Id] = connection;

            // Send retained messages to new connection
            foreach (var msg in _retainedMessages.Values)
            {
                _ = SendToConnectionAsync(connection, msg);
            }

            Log.Debug($"Comms connection added: {connection.Id}");
        }

        /// <summary>
        /// Remove a connection.
        /// </summary>
        public void RemoveConnection(string connectionId)
        {
            _connections.TryRemove(connectionId, out _);
            Log.Debug($"Comms connection removed: {connectionId}");
        }

        /// <summary>
        /// Publish a message to all connected clients.
        /// </summary>
        public async Task PublishAsync(string topic, object? data, bool retain = false)
        {
            var message = new CommsMessage
            {
                Topic = topic,
                Data = data
            };

            if (retain)
            {
                _retainedMessages[topic] = message;
            }

            foreach (var connection in _connections.Values)
            {
                await SendToConnectionAsync(connection, message);
            }
        }

        /// <summary>
        /// Handle incoming message from a connection.
        /// </summary>
        public async Task HandleMessageAsync(string connectionId, CommsMessage message)
        {
            if (!_connections.TryGetValue(connectionId, out var connection))
            {
                return;
            }

            // Handle subscription/unsubscription
            if (message.Topic == "subscribe")
            {
                // TODO: Implement topic-based subscriptions
            }
            else if (message.Topic == "unsubscribe")
            {
                // TODO: Implement topic-based unsubscriptions
            }
            else
            {
                // Forward message to runtime
                Events.Instance.Emit("comms-message", message);
            }

            await Task.CompletedTask;
        }

        private void HandleCommsEvent(object? sender, EventArgs e)
        {
            if (e is CommsEventArgs args)
            {
                _ = PublishAsync(args.Topic, args.Data, args.Retain);
            }
        }

        private void HandleRuntimeEvent(object? sender, EventArgs e)
        {
            if (e is RuntimeEventArgs args)
            {
                var topic = "notification/" + args.Id;
                var data = new Dictionary<string, object?>
                {
                    { "id", args.Id },
                    { "payload", args.Payload }
                };

                _ = PublishAsync(topic, data, args.Retain);
            }
        }

        private static async Task SendToConnectionAsync(CommsConnection connection, CommsMessage message)
        {
            try
            {
                if (connection.SendAsync != null)
                {
                    await connection.SendAsync(message);
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to send message to connection {connection.Id}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Event args for comms events.
    /// </summary>
    public class CommsEventArgs : EventArgs
    {
        public string Topic { get; set; } = string.Empty;
        public object? Data { get; set; }
        public bool Retain { get; set; }
    }

    /// <summary>
    /// Event args for runtime events.
    /// </summary>
    public class RuntimeEventArgs : EventArgs
    {
        public string Id { get; set; } = string.Empty;
        public object? Payload { get; set; }
        public bool Retain { get; set; }
    }
}
// ------------------------------------------------------------
// MAPPING NOTES:
// - WebSocket (ws) → SignalR Hub (to be implemented in Blazor)
// - connections array → _connections ConcurrentDictionary
// - retained object → _retainedMessages ConcurrentDictionary
// - init function → Init method
// - start function → Start method
// - stop function → Stop method
// - publish function → PublishAsync method
// - handleRemoteSubscription → HandleMessageAsync method
// - RED.events.on → Events.Instance.On
// ============================================================
