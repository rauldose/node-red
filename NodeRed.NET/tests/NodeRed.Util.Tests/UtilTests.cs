// Tests for NodeRed.Util module
// These tests verify the translations of @node-red/util/lib files

using Xunit;
using NodeRed.Util;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NodeRed.Util.Tests
{
    /// <summary>
    /// Tests for the Util class (translation of util.js)
    /// </summary>
    public class UtilTests
    {
        [Fact]
        public void GenerateId_ReturnsValidHexString()
        {
            // Arrange & Act
            var id = Util.GenerateId();

            // Assert
            Assert.NotNull(id);
            Assert.Equal(16, id.Length);
            Assert.Matches("^[0-9a-f]{16}$", id);
        }

        [Fact]
        public void GenerateId_ReturnsUniqueIds()
        {
            // Arrange & Act
            var id1 = Util.GenerateId();
            var id2 = Util.GenerateId();

            // Assert
            Assert.NotEqual(id1, id2);
        }

        [Fact]
        public void EnsureString_WithString_ReturnsSameString()
        {
            // Arrange
            var input = "test string";

            // Act
            var result = Util.EnsureString(input);

            // Assert
            Assert.Equal("test string", result);
        }

        [Fact]
        public void EnsureString_WithByteArray_ReturnsUtf8String()
        {
            // Arrange
            var input = new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f }; // "Hello"

            // Act
            var result = Util.EnsureString(input);

            // Assert
            Assert.Equal("Hello", result);
        }

        [Fact]
        public void EnsureString_WithObject_ReturnsJsonString()
        {
            // Arrange
            var input = new { name = "test", value = 123 };

            // Act
            var result = Util.EnsureString(input);

            // Assert
            Assert.Contains("name", result);
            Assert.Contains("test", result);
        }

        [Fact]
        public void EnsureBuffer_WithString_ReturnsUtf8Bytes()
        {
            // Arrange
            var input = "Hello";

            // Act
            var result = Util.EnsureBuffer(input);

            // Assert
            Assert.Equal(new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f }, result);
        }

        [Fact]
        public void EnsureBuffer_WithByteArray_ReturnsSameArray()
        {
            // Arrange
            var input = new byte[] { 1, 2, 3, 4, 5 };

            // Act
            var result = Util.EnsureBuffer(input);

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void CompareObjects_WithEqualPrimitives_ReturnsTrue()
        {
            Assert.True(Util.CompareObjects(123, 123));
            Assert.True(Util.CompareObjects("test", "test"));
            Assert.True(Util.CompareObjects(true, true));
        }

        [Fact]
        public void CompareObjects_WithDifferentPrimitives_ReturnsFalse()
        {
            Assert.False(Util.CompareObjects(123, 456));
            Assert.False(Util.CompareObjects("test", "other"));
            Assert.False(Util.CompareObjects(true, false));
        }

        [Fact]
        public void CompareObjects_WithEqualByteArrays_ReturnsTrue()
        {
            // Arrange
            var arr1 = new byte[] { 1, 2, 3 };
            var arr2 = new byte[] { 1, 2, 3 };

            // Act & Assert
            Assert.True(Util.CompareObjects(arr1, arr2));
        }

        [Fact]
        public void CompareObjects_WithDifferentByteArrays_ReturnsFalse()
        {
            // Arrange
            var arr1 = new byte[] { 1, 2, 3 };
            var arr2 = new byte[] { 1, 2, 4 };

            // Act & Assert
            Assert.False(Util.CompareObjects(arr1, arr2));
        }

        [Fact]
        public void NormalisePropertyExpression_WithSimplePath_ReturnsParts()
        {
            // Arrange
            var expr = "foo.bar.baz";

            // Act
            var result = Util.NormalisePropertyExpression(expr);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal("foo", result[0]);
            Assert.Equal("bar", result[1]);
            Assert.Equal("baz", result[2]);
        }

        [Fact]
        public void NormalisePropertyExpression_WithArrayIndex_ReturnsIntPart()
        {
            // Arrange
            var expr = "foo[0].bar";

            // Act
            var result = Util.NormalisePropertyExpression(expr);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal("foo", result[0]);
            Assert.Equal(0, result[1]);
            Assert.Equal("bar", result[2]);
        }

        [Fact]
        public void NormalisePropertyExpression_WithQuotedProperty_ReturnsParts()
        {
            // Arrange
            var expr = "foo['bar-baz']";

            // Act
            var result = Util.NormalisePropertyExpression(expr);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("foo", result[0]);
            Assert.Equal("bar-baz", result[1]);
        }

        [Fact]
        public void NormalisePropertyExpression_WithEmptyString_ThrowsError()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<NodeRedError>(() => Util.NormalisePropertyExpression(""));
            Assert.Equal("INVALID_EXPR", ex.Code);
        }

        [Fact]
        public void ParseContextStore_WithStorePrefix_ParsesCorrectly()
        {
            // Arrange
            var key = "#:(file)::myKey";

            // Act
            var result = Util.ParseContextStore(key);

            // Assert
            Assert.Equal("file", result.Store);
            Assert.Equal("myKey", result.Key);
        }

        [Fact]
        public void ParseContextStore_WithoutStorePrefix_ReturnsKeyOnly()
        {
            // Arrange
            var key = "myKey";

            // Act
            var result = Util.ParseContextStore(key);

            // Assert
            Assert.Null(result.Store);
            Assert.Equal("myKey", result.Key);
        }

        [Fact]
        public void NormaliseNodeTypeName_WithDashes_ReturnsCamelCase()
        {
            // Arrange
            var name = "my-node-type";

            // Act
            var result = Util.NormaliseNodeTypeName(name);

            // Assert
            Assert.Equal("myNodeType", result);
        }

        [Fact]
        public void NormaliseNodeTypeName_WithSpaces_ReturnsCamelCase()
        {
            // Arrange
            var name = "a random node type";

            // Act
            var result = Util.NormaliseNodeTypeName(name);

            // Assert
            Assert.Equal("aRandomNodeType", result);
        }

        [Fact]
        public void GetMessageProperty_WithMsgPrefix_StripsPrefix()
        {
            // Arrange
            var msg = new Dictionary<string, object> { { "payload", "test" } };

            // Act
            var result = Util.GetMessageProperty(msg, "msg.payload");

            // Assert
            Assert.Equal("test", result);
        }

        [Fact]
        public void GetMessageProperty_WithNestedPath_ReturnsValue()
        {
            // Arrange
            var msg = new Dictionary<string, object>
            {
                {
                    "data", new Dictionary<string, object>
                    {
                        { "nested", "value" }
                    }
                }
            };

            // Act
            var result = Util.GetMessageProperty(msg, "data.nested");

            // Assert
            Assert.Equal("value", result);
        }

        [Fact]
        public void SetMessageProperty_WithSimplePath_SetsValue()
        {
            // Arrange
            var msg = new Dictionary<string, object>();

            // Act
            var result = Util.SetMessageProperty(msg, "payload", "test");

            // Assert
            Assert.True(result);
            Assert.Equal("test", msg["payload"]);
        }

        [Fact]
        public void SetMessageProperty_WithNestedPath_CreatesPath()
        {
            // Arrange
            var msg = new Dictionary<string, object>();

            // Act
            var result = Util.SetMessageProperty(msg, "data.nested", "value");

            // Assert
            Assert.True(result);
            Assert.IsType<Dictionary<string, object>>(msg["data"]);
            var data = (Dictionary<string, object>)msg["data"];
            Assert.Equal("value", data["nested"]);
        }
    }

    /// <summary>
    /// Tests for the Events class (translation of events.js)
    /// </summary>
    public class EventsTests
    {
        [Fact]
        public void On_RegistersListener()
        {
            // Arrange
            var events = Events.Instance;
            events.RemoveAllListeners("test_on");
            var called = false;

            // Act
            events.On("test_on", (sender, args) => called = true);
            events.Emit("test_on");

            // Assert
            Assert.True(called);
            
            // Cleanup
            events.RemoveAllListeners("test_on");
        }

        [Fact]
        public void Once_RegistersOneTimeListener()
        {
            // Arrange
            var events = Events.Instance;
            events.RemoveAllListeners("test_once");
            var callCount = 0;

            // Act
            events.Once("test_once", (sender, args) => callCount++);
            events.Emit("test_once");
            events.Emit("test_once");

            // Assert
            Assert.Equal(1, callCount);
        }

        [Fact]
        public void Emit_ReturnsTrue_WhenListenersExist()
        {
            // Arrange
            var events = Events.Instance;
            events.RemoveAllListeners("test_emit_true");
            events.On("test_emit_true", (sender, args) => { });

            // Act
            var result = events.Emit("test_emit_true");

            // Assert
            Assert.True(result);
            
            // Cleanup
            events.RemoveAllListeners("test_emit_true");
        }

        [Fact]
        public void Emit_ReturnsFalse_WhenNoListeners()
        {
            // Arrange
            var events = Events.Instance;
            events.RemoveAllListeners("test_emit_false");

            // Act
            var result = events.Emit("test_emit_false");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RemoveListener_RemovesListener()
        {
            // Arrange
            var events = Events.Instance;
            events.RemoveAllListeners("test_remove");
            var called = false;
            EventHandler<System.EventArgs> handler = (sender, args) => called = true;
            events.On("test_remove", handler);
            events.RemoveListener("test_remove", handler);

            // Act
            events.Emit("test_remove");

            // Assert
            Assert.False(called);
        }
    }

    /// <summary>
    /// Tests for the Hooks class (translation of hooks.js)
    /// </summary>
    public class HooksTests
    {
        [Fact]
        public void Add_RegistersHook()
        {
            // Arrange
            Hooks.Clear();

            // Act
            Hooks.Add("onSend.test", payload => null);

            // Assert
            Assert.True(Hooks.Has("onSend.test"));
        }

        [Fact]
        public void Add_ThrowsForInvalidHook()
        {
            // Arrange
            Hooks.Clear();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => Hooks.Add("invalidHook", payload => null));
        }

        [Fact]
        public void Remove_RemovesHook()
        {
            // Arrange
            Hooks.Clear();
            Hooks.Add("onSend.test", payload => null);

            // Act
            Hooks.Remove("onSend.test");

            // Assert
            Assert.False(Hooks.Has("onSend.test"));
        }

        [Fact]
        public void Has_ReturnsFalseForUnregisteredHook()
        {
            // Arrange
            Hooks.Clear();

            // Act & Assert
            Assert.False(Hooks.Has("onSend.unregistered"));
        }

        [Fact]
        public void Clear_RemovesAllHooks()
        {
            // Arrange
            Hooks.Clear();
            Hooks.Add("onSend.test1", payload => null);
            Hooks.Add("preRoute.test2", payload => null);

            // Act
            Hooks.Clear();

            // Assert
            Assert.False(Hooks.Has("onSend.test1"));
            Assert.False(Hooks.Has("preRoute.test2"));
        }
    }

    /// <summary>
    /// Tests for the Log class (translation of log.js)
    /// </summary>
    public class LogTests
    {
        [Fact]
        public void LogLevelConstants_MatchOriginal()
        {
            // Assert - these must match the values in Node-RED log.js
            Assert.Equal(10, Log.FATAL);
            Assert.Equal(20, Log.ERROR);
            Assert.Equal(30, Log.WARN);
            Assert.Equal(40, Log.INFO);
            Assert.Equal(50, Log.DEBUG);
            Assert.Equal(60, Log.TRACE);
            Assert.Equal(98, Log.AUDIT);
            Assert.Equal(99, Log.METRIC);
        }
    }
}
