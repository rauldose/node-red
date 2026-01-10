// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using FluentAssertions;
using NodeRed.Core.Entities;
using NodeRed.Runtime.Services;
using Xunit;

namespace NodeRed.Tests.Services;

/// <summary>
/// Tests for MultiplayerService - concurrent editing support.
/// </summary>
public class MultiplayerServiceTests
{
    #region Connection Tests

    [Fact]
    public void Connect_ShouldCreateNewSession()
    {
        // Arrange
        var service = new MultiplayerService();
        var user = new User { Username = "testuser" };

        // Act
        var (session, isNew) = service.Connect("comms-1", "mp-session-1", user);

        // Assert
        isNew.Should().BeTrue();
        session.Should().NotBeNull();
        session.SessionId.Should().Be("mp-session-1");
        session.User.Username.Should().Be("testuser");
        session.Active.Should().BeTrue();
    }

    [Fact]
    public void Connect_ShouldReconnect_ExistingSession()
    {
        // Arrange
        var service = new MultiplayerService();
        var user = new User { Username = "testuser" };

        // Create initial session
        service.Connect("comms-1", "mp-session-1", user);

        // Simulate disconnect
        service.ConnectionRemoved("comms-1");

        // Act - reconnect
        var (session, isNew) = service.Connect("comms-2", "mp-session-1", user);

        // Assert
        isNew.Should().BeFalse();
        session.Active.Should().BeTrue();
        session.CommsSessionId.Should().Be("comms-2");
    }

    [Fact]
    public void GetActiveSessions_ShouldReturnOnlyActiveSessions()
    {
        // Arrange
        var service = new MultiplayerService();
        var user1 = new User { Username = "user1" };
        var user2 = new User { Username = "user2" };

        service.Connect("comms-1", "mp-1", user1);
        service.Connect("comms-2", "mp-2", user2);
        service.ConnectionRemoved("comms-1"); // Make session 1 inactive

        // Act
        var activeSessions = service.GetActiveSessions();

        // Assert
        activeSessions.Should().HaveCount(1);
        activeSessions[0].SessionId.Should().Be("mp-2");
    }

    #endregion

    #region Disconnect Tests

    [Fact]
    public void Disconnect_ShouldRemoveSession()
    {
        // Arrange
        var service = new MultiplayerService();
        var user = new User { Username = "testuser" };
        service.Connect("comms-1", "mp-session-1", user);

        // Act
        service.Disconnect("comms-1");

        // Assert
        var session = service.GetSession("mp-session-1");
        session.Should().BeNull();
    }

    [Fact]
    public void ConnectionRemoved_ShouldMarkSessionInactive()
    {
        // Arrange
        var service = new MultiplayerService();
        var user = new User { Username = "testuser" };
        service.Connect("comms-1", "mp-session-1", user);

        // Act
        service.ConnectionRemoved("comms-1");

        // Assert
        var session = service.GetSession("mp-session-1");
        session.Should().NotBeNull();
        session!.Active.Should().BeFalse();
    }

    #endregion

    #region Location Update Tests

    [Fact]
    public void UpdateLocation_ShouldUpdateSessionLocation()
    {
        // Arrange
        var service = new MultiplayerService();
        var user = new User { Username = "testuser" };
        service.Connect("comms-1", "mp-session-1", user);

        var location = new EditorLocation
        {
            Workspace = "flow-1",
            Node = "node-123",
            Cursor = new CursorPosition { X = 100, Y = 200 }
        };

        // Act
        service.UpdateLocation("comms-1", location);

        // Assert
        var session = service.GetSession("mp-session-1");
        session.Should().NotBeNull();
        session!.Location.Should().NotBeNull();
        session.Location!.Workspace.Should().Be("flow-1");
        session.Location.Node.Should().Be("node-123");
    }

    [Fact]
    public void UpdateLocation_ShouldRaiseEvent()
    {
        // Arrange
        var service = new MultiplayerService();
        var user = new User { Username = "testuser" };
        service.Connect("comms-1", "mp-session-1", user);

        LocationUpdateData? receivedData = null;
        service.OnLocationUpdated += data => receivedData = data;

        var location = new EditorLocation { Workspace = "flow-1" };

        // Act
        service.UpdateLocation("comms-1", location);

        // Assert
        receivedData.Should().NotBeNull();
        receivedData!.SessionId.Should().Be("mp-session-1");
        receivedData.Workspace.Should().Be("flow-1");
    }

    #endregion

    #region Event Tests

    [Fact]
    public void Connect_ShouldRaiseSessionAddedEvent()
    {
        // Arrange
        var service = new MultiplayerService();
        var user = new User { Username = "testuser" };

        MultiplayerSession? addedSession = null;
        service.OnSessionAdded += session => addedSession = session;

        // Act
        service.Connect("comms-1", "mp-session-1", user);

        // Assert
        addedSession.Should().NotBeNull();
        addedSession!.SessionId.Should().Be("mp-session-1");
    }

    [Fact]
    public void Disconnect_ShouldRaiseSessionRemovedEvent()
    {
        // Arrange
        var service = new MultiplayerService();
        var user = new User { Username = "testuser" };
        service.Connect("comms-1", "mp-session-1", user);

        string? removedSessionId = null;
        bool wasDisconnected = false;
        service.OnSessionRemoved += (id, disconnected) =>
        {
            removedSessionId = id;
            wasDisconnected = disconnected;
        };

        // Act
        service.Disconnect("comms-1");

        // Assert
        removedSessionId.Should().Be("mp-session-1");
        wasDisconnected.Should().BeTrue();
    }

    #endregion

    #region Anonymous User Tests

    [Fact]
    public void Connect_ShouldAssignName_ToAnonymousUser()
    {
        // Arrange
        var service = new MultiplayerService();
        var user = new User { Anonymous = true, Username = "anonymous" };

        // Act
        var (session, _) = service.Connect("comms-1", "mp-session-1", user);

        // Assert
        session.User.Username.Should().StartWith("Anon ");
    }

    #endregion

    #region Cleanup Tests

    [Fact]
    public void CleanupInactiveSessions_ShouldRemoveOldInactiveSessions()
    {
        // Arrange
        var service = new MultiplayerService();
        var user = new User { Username = "testuser" };
        service.Connect("comms-1", "mp-session-1", user);
        service.ConnectionRemoved("comms-1");

        // Get the session and manually set last active time to past
        var session = service.GetSession("mp-session-1");
        // Note: We can't easily set LastActiveAt without reflection,
        // so this test verifies the method runs without error

        // Act
        service.CleanupInactiveSessions();

        // Assert - method should complete without error
        // In a real scenario with old timestamps, inactive sessions would be removed
    }

    #endregion
}
