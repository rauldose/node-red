// Tests for NodeRed.EditorApi module
// These tests verify the translations of @node-red/editor-api files

using Xunit;
using NodeRed.EditorApi;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodeRed.EditorApi.Tests
{
    /// <summary>
    /// Tests for the Permissions class (translation of permissions.js)
    /// </summary>
    public class PermissionsTests
    {
        [Fact]
        public void HasPermission_EmptyPermission_ReturnsTrue()
        {
            // Act
            var result = Permissions.HasPermission("*", "");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasPermission_WildcardScope_ReturnsTrue()
        {
            // Act
            var result = Permissions.HasPermission("*", "flows.read");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasPermission_ExactMatch_ReturnsTrue()
        {
            // Act
            var result = Permissions.HasPermission("flows.read", "flows.read");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasPermission_NoMatch_ReturnsFalse()
        {
            // Act
            var result = Permissions.HasPermission("flows.read", "flows.write");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasPermission_ReadScope_MatchesReadPermission()
        {
            // Act
            var result = Permissions.HasPermission("read", "flows.read");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasPermission_ReadScope_DoesNotMatchWritePermission()
        {
            // Act
            var result = Permissions.HasPermission("read", "flows.write");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasPermission_WriteScope_MatchesWritePermission()
        {
            // Act
            var result = Permissions.HasPermission("write", "flows.write");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasPermission_WriteScope_DoesNotMatchReadPermission()
        {
            // Act
            var result = Permissions.HasPermission("write", "flows.read");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasPermission_ScopeList_MatchesAnyPermission()
        {
            // Arrange
            var scopeList = new List<string> { "flows.read", "nodes.read" };

            // Act
            var result = Permissions.HasPermission(scopeList, "nodes.read");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasPermission_EmptyScopeList_ReturnsFalse()
        {
            // Arrange
            var scopeList = new List<string>();

            // Act
            var result = Permissions.HasPermission(scopeList, "flows.read");

            // Assert
            Assert.False(result);
        }
    }

    /// <summary>
    /// Tests for the TokenStore class (translation of tokens.js)
    /// </summary>
    public class TokenStoreTests
    {
        [Fact]
        public void Create_ReturnsTokens()
        {
            // Arrange
            var store = new TokenStore();
            store.Init(null, null);

            // Act
            var tokens = store.Create("testuser", "editor", new List<string> { "*" });

            // Assert
            Assert.NotNull(tokens);
            Assert.NotEmpty(tokens.AccessToken);
            Assert.NotEmpty(tokens.RefreshToken);
            Assert.True(tokens.ExpiresIn > 0);
        }

        [Fact]
        public void Validate_ValidToken_ReturnsUser()
        {
            // Arrange
            var store = new TokenStore();
            store.Init(null, null);
            var tokens = store.Create("testuser", "editor", new List<string> { "*" });

            // Act
            var user = store.Validate(tokens.AccessToken);

            // Assert
            Assert.NotNull(user);
            Assert.Equal("testuser", user.Username);
        }

        [Fact]
        public void Validate_InvalidToken_ReturnsNull()
        {
            // Arrange
            var store = new TokenStore();
            store.Init(null, null);

            // Act
            var user = store.Validate("invalid-token");

            // Assert
            Assert.Null(user);
        }

        [Fact]
        public void Revoke_RemovesToken()
        {
            // Arrange
            var store = new TokenStore();
            store.Init(null, null);
            var tokens = store.Create("testuser", "editor", new List<string> { "*" });

            // Act
            store.Revoke(tokens.AccessToken);
            var user = store.Validate(tokens.AccessToken);

            // Assert
            Assert.Null(user);
        }
    }

    /// <summary>
    /// Tests for the CommsHandler class (translation of comms.js)
    /// </summary>
    public class CommsHandlerTests
    {
        [Fact]
        public void AddConnection_StoresConnection()
        {
            // Arrange
            var handler = new CommsHandler();
            var connection = new CommsConnection
            {
                Id = "test-connection-1",
                SendAsync = async msg => await Task.CompletedTask
            };

            // Act
            handler.AddConnection(connection);

            // Assert - No exception means success
            // The connection is stored internally
        }

        [Fact]
        public void RemoveConnection_RemovesConnection()
        {
            // Arrange
            var handler = new CommsHandler();
            var connection = new CommsConnection
            {
                Id = "test-connection-1",
                SendAsync = async msg => await Task.CompletedTask
            };
            handler.AddConnection(connection);

            // Act
            handler.RemoveConnection("test-connection-1");

            // Assert - No exception means success
        }

        [Fact]
        public async Task PublishAsync_SendsToConnections()
        {
            // Arrange
            var handler = new CommsHandler();
            CommsMessage? receivedMessage = null;

            var connection = new CommsConnection
            {
                Id = "test-connection-1",
                SendAsync = async msg =>
                {
                    receivedMessage = msg;
                    await Task.CompletedTask;
                }
            };
            handler.AddConnection(connection);

            // Act
            await handler.PublishAsync("test/topic", new { data = "test" });

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal("test/topic", receivedMessage.Topic);
        }

        [Fact]
        public void Start_And_Stop_DoNotThrow()
        {
            // Arrange
            var handler = new CommsHandler();

            // Act & Assert - No exceptions
            handler.Start();
            handler.Stop();
        }
    }

    /// <summary>
    /// Tests for the EditorApi class (translation of index.js)
    /// </summary>
    public class EditorApiTests
    {
        [Fact]
        public void Init_WithSettings_DoesNotThrow()
        {
            // Arrange
            var api = new EditorApi();
            var settings = new NodeRed.Runtime.Settings();
            settings.Init(new Dictionary<string, object?>());
            var storage = new NodeRed.Runtime.Storage();
            var flowsManager = new NodeRed.Runtime.FlowsManager();

            // Act - Should not throw
            var exception = Record.Exception(() => api.Init(settings, storage, flowsManager));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public async Task StartAsync_DoesNotThrow()
        {
            // Arrange
            var api = new EditorApi();
            var settings = new NodeRed.Runtime.Settings();
            settings.Init(new Dictionary<string, object?>());
            var storage = new NodeRed.Runtime.Storage();
            var flowsManager = new NodeRed.Runtime.FlowsManager();
            api.Init(settings, storage, flowsManager);

            // Act & Assert - No exceptions
            await api.StartAsync();
        }

        [Fact]
        public async Task StopAsync_DoesNotThrow()
        {
            // Arrange
            var api = new EditorApi();
            var settings = new NodeRed.Runtime.Settings();
            settings.Init(new Dictionary<string, object?>());
            var storage = new NodeRed.Runtime.Storage();
            var flowsManager = new NodeRed.Runtime.FlowsManager();
            api.Init(settings, storage, flowsManager);
            await api.StartAsync();

            // Act & Assert - No exceptions
            await api.StopAsync();
        }
    }
}
