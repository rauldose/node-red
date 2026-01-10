// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using FluentAssertions;
using NodeRed.Core.Utilities;
using Xunit;

namespace NodeRed.Tests.Utilities;

public class PropertyUtilsTests
{
    [Theory]
    [InlineData("foo", new object[] { "foo" })]
    [InlineData("foo.bar", new object[] { "foo", "bar" })]
    [InlineData("foo.bar.baz", new object[] { "foo", "bar", "baz" })]
    public void NormalisePropertyExpression_SimpleProperties(string expr, object[] expected)
    {
        // Act
        var result = PropertyUtils.NormalisePropertyExpression(expr);
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("foo[0]", new object[] { "foo", 0 })]
    [InlineData("foo[0].bar", new object[] { "foo", 0, "bar" })]
    [InlineData("foo[0][1]", new object[] { "foo", 0, 1 })]
    public void NormalisePropertyExpression_ArrayAccess(string expr, object[] expected)
    {
        // Act
        var result = PropertyUtils.NormalisePropertyExpression(expr);
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("foo['bar']", new object[] { "foo", "bar" })]
    [InlineData("foo[\"bar\"]", new object[] { "foo", "bar" })]
    public void NormalisePropertyExpression_QuotedProperties(string expr, object[] expected)
    {
        // Act
        var result = PropertyUtils.NormalisePropertyExpression(expr);
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".foo")]
    [InlineData("foo.")]
    [InlineData("[foo]")]
    [InlineData("foo[ ]")]
    [InlineData("foo bar")]
    public void NormalisePropertyExpression_InvalidExpressions_ThrowsError(string expr)
    {
        // Act & Assert
        var act = () => PropertyUtils.NormalisePropertyExpression(expr);
        act.Should().Throw<Exception>().WithMessage("Invalid property expression*");
    }

    [Fact]
    public void GetMessageProperty_ReturnsProperty()
    {
        // Arrange
        var msg = new Dictionary<string, object?>
        {
            { "payload", "test" },
            { "topic", "hello" }
        };
        
        // Act & Assert
        PropertyUtils.GetMessageProperty(msg, "payload").Should().Be("test");
        PropertyUtils.GetMessageProperty(msg, "topic").Should().Be("hello");
        PropertyUtils.GetMessageProperty(msg, "msg.payload").Should().Be("test"); // With msg. prefix
    }

    [Fact]
    public void GetMessageProperty_NestedProperty()
    {
        // Arrange
        var msg = new Dictionary<string, object?>
        {
            { "payload", new Dictionary<string, object?> { { "value", 42 } } }
        };
        
        // Act
        var result = PropertyUtils.GetMessageProperty(msg, "payload.value");
        
        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void SetMessageProperty_SetsSimpleProperty()
    {
        // Arrange
        var msg = new Dictionary<string, object?>();
        
        // Act
        var result = PropertyUtils.SetMessageProperty(msg, "payload", "test");
        
        // Assert
        result.Should().BeTrue();
        msg["payload"].Should().Be("test");
    }

    [Fact]
    public void SetMessageProperty_SetsNestedProperty()
    {
        // Arrange
        var msg = new Dictionary<string, object?>
        {
            { "payload", new Dictionary<string, object?>() }
        };
        
        // Act
        var result = PropertyUtils.SetMessageProperty(msg, "payload.value", 42);
        
        // Assert
        result.Should().BeTrue();
        ((Dictionary<string, object?>)msg["payload"]!)["value"].Should().Be(42);
    }

    [Fact]
    public void SetMessageProperty_DeletesProperty_WhenValueIsNull()
    {
        // Arrange
        var msg = new Dictionary<string, object?>
        {
            { "payload", "test" }
        };
        
        // Act
        var result = PropertyUtils.SetMessageProperty(msg, "payload", null, false);
        
        // Assert
        result.Should().BeTrue();
        msg.Should().NotContainKey("payload");
    }

    [Theory]
    [InlineData("#:(memory)::foo", "memory", "foo")]
    [InlineData("#:(file)::bar.baz", "file", "bar.baz")]
    [InlineData("foo", null, "foo")]
    [InlineData("foo.bar", null, "foo.bar")]
    public void ParseContextStore_ParsesCorrectly(string key, string? expectedStore, string expectedKey)
    {
        // Act
        var (store, parsedKey) = PropertyUtils.ParseContextStore(key);
        
        // Assert
        store.Should().Be(expectedStore);
        parsedKey.Should().Be(expectedKey);
    }

    [Theory]
    [InlineData("a-random node type", "aRandomNodeType")]
    [InlineData("http in", "httpIn")]
    [InlineData("my_node", "myNode")]
    [InlineData("SimpleNode", "simpleNode")]
    public void NormaliseNodeTypeName_NormalizesCorrectly(string name, string expected)
    {
        // Act
        var result = PropertyUtils.NormaliseNodeTypeName(name);
        
        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void EvaluateEnvProperty_SubstitutesEnvVar()
    {
        // Arrange
        Func<string, string?> getEnv = name => name == "WHO" ? "Joe" : null;
        
        // Act & Assert
        PropertyUtils.EvaluateEnvProperty("${WHO}", getEnv).Should().Be("Joe");
        PropertyUtils.EvaluateEnvProperty("Hello ${WHO}!", getEnv).Should().Be("Hello Joe!");
    }

    [Theory]
    [InlineData("str", "hello", "hello")]
    [InlineData("num", "42", 42.0)]
    [InlineData("bool", "true", true)]
    [InlineData("bool", "false", false)]
    public void EvaluateNodeProperty_EvaluatesTypes(string type, string value, object expected)
    {
        // Act
        var result = PropertyUtils.EvaluateNodeProperty(value, type);
        
        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void EvaluateNodeProperty_Json_ParsesObject()
    {
        // Act
        var result = PropertyUtils.EvaluateNodeProperty("{\"foo\":\"bar\"}", "json");
        
        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void EvaluateNodeProperty_Date_ReturnsTimestamp()
    {
        // Act
        var result = PropertyUtils.EvaluateNodeProperty("", "date");
        
        // Assert
        result.Should().BeOfType<long>();
        ((long)result!).Should().BeGreaterThan(0);
    }

    [Fact]
    public void EvaluateNodeProperty_Msg_GetsMessageProperty()
    {
        // Arrange
        var msg = new Dictionary<string, object?> { { "payload", "test" } };
        
        // Act
        var result = PropertyUtils.EvaluateNodeProperty("payload", "msg", msg);
        
        // Assert
        result.Should().Be("test");
    }
}
