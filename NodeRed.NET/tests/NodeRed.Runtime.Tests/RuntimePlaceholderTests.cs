// Tests for NodeRed.Runtime module
// These tests verify the translations of @node-red/runtime/lib files

using Xunit;
using NodeRed.Runtime;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodeRed.Runtime.Tests
{
    /// <summary>
    /// Tests for the Settings class (translation of settings.js)
    /// </summary>
    public class SettingsTests
    {
        [Fact]
        public void Init_WithSettings_StoresLocalSettings()
        {
            // Arrange
            var settings = new Settings();
            var localSettings = new Dictionary<string, object?>
            {
                { "httpAdminRoot", "/admin" },
                { "httpNodeRoot", "/api" }
            };

            // Act
            settings.Init(localSettings);

            // Assert
            Assert.True(settings.HasProperty("httpAdminRoot"));
            Assert.True(settings.HasProperty("httpNodeRoot"));
        }

        [Fact]
        public void Get_WithLocalProperty_ReturnsValue()
        {
            // Arrange
            var settings = new Settings();
            settings.Init(new Dictionary<string, object?>
            {
                { "testProp", "testValue" }
            });

            // Act
            var result = settings.Get("testProp");

            // Assert
            Assert.Equal("testValue", result?.ToString());
        }

        [Fact]
        public void Available_BeforeLoad_ReturnsFalse()
        {
            // Arrange
            var settings = new Settings();
            settings.Init(new Dictionary<string, object?>());

            // Act & Assert
            Assert.False(settings.Available());
        }

        [Fact]
        public void Reset_ClearsAllSettings()
        {
            // Arrange
            var settings = new Settings();
            settings.Init(new Dictionary<string, object?>
            {
                { "prop", "value" }
            });

            // Act
            settings.Reset();

            // Assert
            Assert.False(settings.HasProperty("prop"));
        }

        [Fact]
        public void RegisterNodeSettings_WithValidPrefix_Succeeds()
        {
            // Arrange
            var settings = new Settings();
            settings.Init(new Dictionary<string, object?>());

            // Act & Assert - should not throw
            settings.RegisterNodeSettings("my-node", new Dictionary<string, NodeSettingsOption>
            {
                { "myNodeSetting", new NodeSettingsOption { Exportable = true, Value = "default" } }
            });
        }

        [Fact]
        public void RegisterNodeSettings_WithInvalidPrefix_ThrowsException()
        {
            // Arrange
            var settings = new Settings();
            settings.Init(new Dictionary<string, object?>());

            // Act & Assert
            Assert.Throws<System.ArgumentException>(() =>
                settings.RegisterNodeSettings("my-node", new Dictionary<string, NodeSettingsOption>
                {
                    { "wrongPrefix", new NodeSettingsOption { Exportable = true } }
                }));
        }
    }

    /// <summary>
    /// Tests for the Storage class (translation of storage/index.js)
    /// </summary>
    public class StorageTests
    {
        [Fact]
        public void Storage_IsMalicious_DetectsPathTraversal()
        {
            // This test verifies the ForbiddenException is thrown for malicious paths
            // Arrange
            var storage = new Storage();

            // Act & Assert - GetLibraryEntryAsync should throw for malicious path
            Assert.ThrowsAsync<ForbiddenException>(async () =>
                await storage.GetLibraryEntryAsync("flows", "../../../etc/passwd"));
        }
    }

    /// <summary>
    /// Tests for the FlowsManager class (translation of flows/index.js)
    /// </summary>
    public class FlowsManagerTests
    {
        [Fact]
        public void Init_SetsStartedToFalse()
        {
            // Arrange
            var manager = new FlowsManager();
            var settings = new Settings();
            settings.Init(new Dictionary<string, object?>());
            var storage = new Storage();

            // Act
            manager.Init(settings, storage);

            // Assert
            Assert.False(manager.Started);
            Assert.Equal(FlowState.Stop, manager.State);
        }

        [Fact]
        public void GetFlows_BeforeLoad_ReturnsNull()
        {
            // Arrange
            var manager = new FlowsManager();

            // Act
            var result = manager.GetFlows();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void IsDeliveryModeAsync_ByDefault_ReturnsTrue()
        {
            // Arrange
            var manager = new FlowsManager();

            // Act
            var result = manager.IsDeliveryModeAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task AddFlowAsync_WithoutNodes_ThrowsException()
        {
            // Arrange
            var manager = new FlowsManager();
            var settings = new Settings();
            settings.Init(new Dictionary<string, object?>());
            var storage = new Storage();
            manager.Init(settings, storage);

            var flowInput = new FlowInput
            {
                Label = "Test Flow",
                Nodes = null!
            };

            // Act & Assert
            await Assert.ThrowsAsync<System.ArgumentException>(async () =>
                await manager.AddFlowAsync(flowInput, null));
        }

        [Fact]
        public async Task RemoveFlowAsync_GlobalFlow_ThrowsException()
        {
            // Arrange
            var manager = new FlowsManager();
            var settings = new Settings();
            settings.Init(new Dictionary<string, object?>());
            var storage = new Storage();
            manager.Init(settings, storage);

            // Act & Assert
            await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await manager.RemoveFlowAsync("global", null));
        }
    }

    /// <summary>
    /// Tests for the Flow class
    /// </summary>
    public class FlowTests
    {
        [Fact]
        public void Flow_Constructor_SetsId()
        {
            // Arrange
            var config = new FlowConfiguration();

            // Act
            var flow = new Flow("test-flow", config);

            // Assert
            Assert.Equal("test-flow", flow.Id);
        }

        [Fact]
        public void GetActiveNodes_Initially_ReturnsEmptyDictionary()
        {
            // Arrange
            var config = new FlowConfiguration();
            var flow = new Flow("test-flow", config);

            // Act
            var nodes = flow.GetActiveNodes();

            // Assert
            Assert.Empty(nodes);
        }
    }
}
