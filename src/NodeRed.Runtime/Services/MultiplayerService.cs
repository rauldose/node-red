// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using NodeRed.Core.Entities;

namespace NodeRed.Runtime.Services;

/// <summary>
/// Service for managing multiplayer editing sessions.
/// Based on packages/node_modules/@node-red/runtime/lib/multiplayer/index.js
/// </summary>
public class MultiplayerService
{
    private readonly Dictionary<string, MultiplayerSession> _sessions = new();
    private readonly Dictionary<string, string> _commsToSession = new(); // Maps comms session to multiplayer session
    private readonly object _lock = new();
    private readonly TimeSpan _idleTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Event raised when a session is added.
    /// </summary>
    public event Action<MultiplayerSession>? OnSessionAdded;

    /// <summary>
    /// Event raised when a session is removed.
    /// </summary>
    public event Action<string, bool>? OnSessionRemoved; // sessionId, wasDisconnected

    /// <summary>
    /// Event raised when a session's location is updated.
    /// </summary>
    public event Action<LocationUpdateData>? OnLocationUpdated;

    /// <summary>
    /// Gets all active sessions.
    /// </summary>
    public IReadOnlyList<MultiplayerSession> GetActiveSessions()
    {
        lock (_lock)
        {
            return _sessions.Values.Where(s => s.Active).ToList();
        }
    }

    /// <summary>
    /// Gets a session by ID.
    /// </summary>
    public MultiplayerSession? GetSession(string sessionId)
    {
        lock (_lock)
        {
            _sessions.TryGetValue(sessionId, out var session);
            return session;
        }
    }

    /// <summary>
    /// Handles a new connection or reconnection.
    /// </summary>
    /// <param name="commsSessionId">The SignalR/WebSocket connection ID.</param>
    /// <param name="multiplayerSessionId">The multiplayer session ID.</param>
    /// <param name="user">The user connecting.</param>
    /// <returns>The session and whether it's a new session.</returns>
    public (MultiplayerSession session, bool isNew) Connect(string commsSessionId, string multiplayerSessionId, User user)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(multiplayerSessionId, out var session))
            {
                // Brand new session
                session = new MultiplayerSession
                {
                    SessionId = multiplayerSessionId,
                    User = user.Anonymous ? EnsureAnonymousName(user) : user,
                    Active = true,
                    CommsSessionId = commsSessionId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastActiveAt = DateTimeOffset.UtcNow
                };
                _sessions[multiplayerSessionId] = session;
                _commsToSession[commsSessionId] = multiplayerSessionId;

                OnSessionAdded?.Invoke(session);
                return (session, true);
            }
            else
            {
                // Reconnecting to existing session
                session.Active = true;
                session.CommsSessionId = commsSessionId;
                session.LastActiveAt = DateTimeOffset.UtcNow;
                _commsToSession[commsSessionId] = multiplayerSessionId;

                OnSessionAdded?.Invoke(session);
                return (session, false);
            }
        }
    }

    /// <summary>
    /// Handles an explicit disconnect request.
    /// </summary>
    /// <param name="commsSessionId">The SignalR/WebSocket connection ID.</param>
    public void Disconnect(string commsSessionId)
    {
        lock (_lock)
        {
            if (_commsToSession.TryGetValue(commsSessionId, out var sessionId))
            {
                _commsToSession.Remove(commsSessionId);
                _sessions.Remove(sessionId);

                OnSessionRemoved?.Invoke(sessionId, true);
            }
        }
    }

    /// <summary>
    /// Handles a connection being removed (e.g., network disconnect).
    /// Marks session as inactive and starts idle timeout.
    /// </summary>
    /// <param name="commsSessionId">The SignalR/WebSocket connection ID.</param>
    public void ConnectionRemoved(string commsSessionId)
    {
        lock (_lock)
        {
            if (_commsToSession.TryGetValue(commsSessionId, out var sessionId))
            {
                _commsToSession.Remove(commsSessionId);

                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    session.Active = false;

                    // Start idle timeout
                    Task.Delay(_idleTimeout).ContinueWith(_ =>
                    {
                        lock (_lock)
                        {
                            if (_sessions.TryGetValue(sessionId, out var s) && !s.Active)
                            {
                                _sessions.Remove(sessionId);
                            }
                        }
                    });

                    OnSessionRemoved?.Invoke(sessionId, false);
                }
            }
        }
    }

    /// <summary>
    /// Updates a session's location.
    /// </summary>
    /// <param name="commsSessionId">The SignalR/WebSocket connection ID.</param>
    /// <param name="location">The new location.</param>
    /// <param name="user">Updated user info (for detecting login changes).</param>
    public void UpdateLocation(string commsSessionId, EditorLocation location, User? user = null)
    {
        lock (_lock)
        {
            if (!_commsToSession.TryGetValue(commsSessionId, out var sessionId))
            {
                return;
            }

            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return;
            }

            // Check if user login status changed
            if (user != null && session.User.Anonymous != user.Anonymous)
            {
                session.User = user;
                OnSessionAdded?.Invoke(session); // Re-announce with updated user
            }

            session.Location = location;
            session.LastActiveAt = DateTimeOffset.UtcNow;

            OnLocationUpdated?.Invoke(new LocationUpdateData
            {
                SessionId = sessionId,
                Workspace = location.Workspace,
                Node = location.Node,
                Cursor = location.Cursor
            });
        }
    }

    /// <summary>
    /// Gets the comms session ID for a multiplayer session.
    /// </summary>
    public string? GetCommsSessionId(string multiplayerSessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(multiplayerSessionId, out var session))
            {
                return session.CommsSessionId;
            }
            return null;
        }
    }

    /// <summary>
    /// Ensures an anonymous user has a unique display name.
    /// </summary>
    private User EnsureAnonymousName(User user)
    {
        if (string.IsNullOrEmpty(user.Username) || user.Username == "anonymous")
        {
            var random = new Random();
            user.Username = $"Anon {random.Next(100)}";
        }
        return user;
    }

    /// <summary>
    /// Cleans up inactive sessions that have exceeded the idle timeout.
    /// </summary>
    public void CleanupInactiveSessions()
    {
        lock (_lock)
        {
            var cutoff = DateTimeOffset.UtcNow - _idleTimeout;
            var sessionsToRemove = _sessions.Values
                .Where(s => !s.Active && s.LastActiveAt < cutoff)
                .Select(s => s.SessionId)
                .ToList();

            foreach (var sessionId in sessionsToRemove)
            {
                _sessions.Remove(sessionId);
            }
        }
    }
}
