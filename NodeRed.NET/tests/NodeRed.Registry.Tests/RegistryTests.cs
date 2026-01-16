// Tests for NodeRed.Registry module
// These tests verify the translations of @node-red/registry/lib files

using Xunit;
using NodeRed.Registry;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodeRed.Registry.Tests
{
    /// <summary>
    /// Tests for the Registry class (translation of registry.js)
    /// </summary>
    public class RegistryTests
    {
        [Fact]
        public void GetModuleFromSetId_ReturnsModuleName()
        {
            // Act
            var result = Registry.GetModuleFromSetId("node-red/inject");

            // Assert
            Assert.Equal("node-red", result);
        }

        [Fact]
        public void GetModuleFromSetId_WithScopedModule_ReturnsFullScope()
        {
            // Act
            var result = Registry.GetModuleFromSetId("@scope/module/node");

            // Assert
            Assert.Equal("@scope/module", result);
        }

        [Fact]
        public void GetNodeFromSetId_ReturnsNodeName()
        {
            // Act
            var result = Registry.GetNodeFromSetId("node-red/inject");

            // Assert
            Assert.Equal("inject", result);
        }

        [Fact]
        public void FilterNodeInfo_CreatesNodeInfo()
        {
            // Arrange
            var config = new NodeConfig
            {
                Id = "node-red/inject",
                Name = "inject",
                Module = "node-red",
                Types = new List<string> { "inject" },
                Enabled = true,
                Local = false,
                User = true
            };

            // Act
            var result = Registry.FilterNodeInfo(config);

            // Assert
            Assert.Equal("node-red/inject", result.Id);
            Assert.Equal("inject", result.Name);
            Assert.Equal("node-red", result.Module);
            Assert.True(result.Enabled);
            Assert.True(result.User);
        }

        [Fact]
        public void Clear_RemovesAllData()
        {
            // Arrange
            var registry = new Registry();
            var settings = new NodeRed.Runtime.Settings();
            settings.Init(new Dictionary<string, object?>());
            registry.Init(settings);

            // Add a module
            registry.AddModule(new ModuleConfig
            {
                Name = "test-module",
                Version = "1.0.0",
                Nodes = new Dictionary<string, NodeConfig>
                {
                    { "test-node", new NodeConfig
                        {
                            Id = "test-module/test-node",
                            Name = "test-node",
                            Types = new List<string> { "test-type" }
                        }
                    }
                }
            });

            // Act
            registry.Clear();

            // Assert
            Assert.Null(registry.GetModule("test-module"));
        }

        [Fact]
        public void AddModule_RegistersModule()
        {
            // Arrange
            var registry = new Registry();
            var settings = new NodeRed.Runtime.Settings();
            settings.Init(new Dictionary<string, object?>());
            registry.Init(settings);

            var module = new ModuleConfig
            {
                Name = "test-module",
                Version = "1.0.0",
                Nodes = new Dictionary<string, NodeConfig>
                {
                    { "test-node", new NodeConfig
                        {
                            Id = "test-module/test-node",
                            Name = "test-node",
                            Types = new List<string> { "test-type" }
                        }
                    }
                }
            };

            // Act
            registry.AddModule(module);

            // Assert
            var result = registry.GetModule("test-module");
            Assert.NotNull(result);
            Assert.Equal("1.0.0", result.Version);
        }

        [Fact]
        public void GetNodeInfo_ReturnsInfo_WhenExists()
        {
            // Arrange
            var registry = new Registry();
            var settings = new NodeRed.Runtime.Settings();
            settings.Init(new Dictionary<string, object?>());
            registry.Init(settings);

            registry.AddModule(new ModuleConfig
            {
                Name = "test-module",
                Version = "2.0.0",
                Nodes = new Dictionary<string, NodeConfig>
                {
                    { "my-node", new NodeConfig
                        {
                            Id = "test-module/my-node",
                            Name = "my-node",
                            Module = "test-module",
                            Types = new List<string> { "my-type" },
                            Enabled = true
                        }
                    }
                }
            });

            // Act
            var result = registry.GetNodeInfo("test-module/my-node");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("my-node", result.Name);
            Assert.Equal("2.0.0", result.Version);
        }

        [Fact]
        public void GetNodeInfo_ReturnsNull_WhenNotExists()
        {
            // Arrange
            var registry = new Registry();
            var settings = new NodeRed.Runtime.Settings();
            settings.Init(new Dictionary<string, object?>());
            registry.Init(settings);

            // Act
            var result = registry.GetNodeInfo("nonexistent/node");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetNodeList_ReturnsAllNodes()
        {
            // Arrange
            var registry = new Registry();
            var settings = new NodeRed.Runtime.Settings();
            settings.Init(new Dictionary<string, object?>());
            registry.Init(settings);

            registry.AddModule(new ModuleConfig
            {
                Name = "module1",
                Version = "1.0.0",
                Nodes = new Dictionary<string, NodeConfig>
                {
                    { "node1", new NodeConfig { Id = "module1/node1", Name = "node1", Types = new List<string> { "type1" } } }
                }
            });

            registry.AddModule(new ModuleConfig
            {
                Name = "module2",
                Version = "2.0.0",
                Nodes = new Dictionary<string, NodeConfig>
                {
                    { "node2", new NodeConfig { Id = "module2/node2", Name = "node2", Types = new List<string> { "type2" } } }
                }
            });

            // Act
            var result = registry.GetNodeList();

            // Assert
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void GetTypeId_ReturnsId_WhenExists()
        {
            // Arrange
            var registry = new Registry();
            var settings = new NodeRed.Runtime.Settings();
            settings.Init(new Dictionary<string, object?>());
            registry.Init(settings);

            registry.AddModule(new ModuleConfig
            {
                Name = "test-module",
                Version = "1.0.0",
                Nodes = new Dictionary<string, NodeConfig>
                {
                    { "test-node", new NodeConfig
                        {
                            Id = "test-module/test-node",
                            Name = "test-node",
                            Types = new List<string> { "my-custom-type" }
                        }
                    }
                }
            });

            // Act
            var result = registry.GetTypeId("my-custom-type");

            // Assert
            Assert.Equal("test-module/test-node", result);
        }

        [Fact]
        public void GetTypeId_ReturnsNull_WhenNotExists()
        {
            // Arrange
            var registry = new Registry();
            var settings = new NodeRed.Runtime.Settings();
            settings.Init(new Dictionary<string, object?>());
            registry.Init(settings);

            // Act
            var result = registry.GetTypeId("nonexistent-type");

            // Assert
            Assert.Null(result);
        }
    }

    /// <summary>
    /// Tests for the Installer class (translation of installer.js)
    /// </summary>
    public class InstallerTests
    {
        [Fact]
        public void Init_SetsInstallerEnabledToFalse()
        {
            // Arrange
            var installer = new Installer();
            var settings = new NodeRed.Runtime.Settings();
            settings.Init(new Dictionary<string, object?>());

            // Act
            installer.Init(settings);

            // Assert - Before CheckPrereq, should be false
            Assert.False(installer.InstallerEnabled);
        }

        [Fact]
        public async Task InstallModuleAsync_WithInvalidName_ThrowsException()
        {
            // Arrange
            var installer = new Installer();
            var settings = new NodeRed.Runtime.Settings();
            settings.Init(new Dictionary<string, object?>());
            installer.Init(settings);

            // Act & Assert
            await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await installer.InstallModuleAsync("invalid module with spaces"));
        }

        [Fact]
        public async Task UninstallModuleAsync_WithInvalidName_ThrowsException()
        {
            // Arrange
            var installer = new Installer();
            var settings = new NodeRed.Runtime.Settings();
            settings.Init(new Dictionary<string, object?>());
            installer.Init(settings);

            // Act & Assert
            await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await installer.UninstallModuleAsync("module;with;semicolons"));
        }
    }

    /// <summary>
    /// Tests for the Loader class (translation of loader.js)
    /// </summary>
    public class LoaderTests
    {
        [Fact]
        public void GetNodeHelp_ReturnsHelp_WhenLangExists()
        {
            // Arrange
            var loader = new Loader();
            var node = new NodeConfig
            {
                Name = "test-node",
                Help = new Dictionary<string, string>
                {
                    { "en-US", "<p>Help in English</p>" },
                    { "de", "<p>Hilfe auf Deutsch</p>" }
                }
            };

            // Act
            var result = loader.GetNodeHelp(node, "en-US");

            // Assert
            Assert.Equal("<p>Help in English</p>", result);
        }

        [Fact]
        public void GetNodeHelp_FallsBackToBaseLang()
        {
            // Arrange
            var loader = new Loader();
            var node = new NodeConfig
            {
                Name = "test-node",
                Help = new Dictionary<string, string>
                {
                    { "de", "<p>Hilfe auf Deutsch</p>" }
                }
            };

            // Act
            var result = loader.GetNodeHelp(node, "de-AT");

            // Assert
            Assert.Equal("<p>Hilfe auf Deutsch</p>", result);
        }

        [Fact]
        public void GetNodeHelp_ReturnsNull_WhenNoHelp()
        {
            // Arrange
            var loader = new Loader();
            var node = new NodeConfig
            {
                Name = "test-node",
                Help = null
            };

            // Act
            var result = loader.GetNodeHelp(node, "en-US");

            // Assert
            Assert.Null(result);
        }
    }
}
