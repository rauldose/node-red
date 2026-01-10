// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using FluentAssertions;
using NodeRed.Core.Entities;
using NodeRed.Core.Utilities;
using Xunit;

namespace NodeRed.Tests.Utilities;

public class MessageUtilsTests
{
    [Fact]
    public void GenerateId_ReturnsHexString()
    {
        // Act
        var id = MessageUtils.GenerateId();
        
        // Assert
        id.Should().NotBeNullOrEmpty();
        id.Should().HaveLength(16);
        id.Should().MatchRegex("^[a-f0-9]+$");
    }

    [Fact]
    public void GenerateId_ReturnsUniqueIds()
    {
        // Act
        var ids = Enumerable.Range(0, 100).Select(_ => MessageUtils.GenerateId()).ToList();
        
        // Assert
        ids.Distinct().Count().Should().Be(100);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("hello", "hello")]
    [InlineData(42, "42")]
    [InlineData(true, "True")]
    public void EnsureString_ConvertsToString(object? input, string expected)
    {
        // Act
        var result = MessageUtils.EnsureString(input);
        
        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void EnsureString_ConvertsBufferToString()
    {
        // Arrange
        var buffer = new byte[] { 72, 101, 108, 108, 111 }; // "Hello"
        
        // Act
        var result = MessageUtils.EnsureString(buffer);
        
        // Assert
        result.Should().Be("Hello");
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    public void EnsureBuffer_ConvertsStringToBuffer(string input)
    {
        // Act
        var result = MessageUtils.EnsureBuffer(input);
        
        // Assert
        result.Should().NotBeNull();
        MessageUtils.EnsureString(result).Should().Be(input);
    }

    [Fact]
    public void EnsureBuffer_ReturnsBufferUnchanged()
    {
        // Arrange
        var buffer = new byte[] { 1, 2, 3 };
        
        // Act
        var result = MessageUtils.EnsureBuffer(buffer);
        
        // Assert
        result.Should().BeSameAs(buffer);
    }

    [Fact]
    public void CloneMessage_ClonesMessage()
    {
        // Arrange
        var original = new NodeMessage
        {
            Payload = "test",
            Topic = "my/topic"
        };
        original.Properties["custom"] = "value";
        
        // Act
        var clone = MessageUtils.CloneMessage(original);
        
        // Assert
        clone.Should().NotBeSameAs(original);
        clone.Topic.Should().Be(original.Topic);
        clone.Properties.Should().ContainKey("custom");
        clone.Properties["custom"].Should().Be("value");
    }

    [Fact]
    public void CloneMessage_GeneratesNewId()
    {
        // Arrange
        var original = new NodeMessage { Id = "original-id" };
        
        // Act
        var clone = MessageUtils.CloneMessage(original);
        
        // Assert
        clone.Id.Should().NotBe(original.Id);
        clone.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CloneMessage_DeepClonesPayload()
    {
        // Arrange
        var original = new NodeMessage
        {
            Payload = new Dictionary<string, object?> { { "key", "value" } }
        };
        
        // Act
        var clone = MessageUtils.CloneMessage(original);
        
        // Assert - Modifying clone shouldn't affect original
        clone.Payload.Should().NotBeSameAs(original.Payload);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(1, 1, true)]
    [InlineData("hello", "hello", true)]
    [InlineData(1, 2, false)]
    [InlineData("hello", "world", false)]
    [InlineData(null, 1, false)]
    public void CompareObjects_ComparesSimpleTypes(object? obj1, object? obj2, bool expected)
    {
        // Act
        var result = MessageUtils.CompareObjects(obj1, obj2);
        
        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CompareObjects_ComparesByteArrays()
    {
        // Arrange
        var arr1 = new byte[] { 1, 2, 3 };
        var arr2 = new byte[] { 1, 2, 3 };
        var arr3 = new byte[] { 1, 2, 4 };
        
        // Act & Assert
        MessageUtils.CompareObjects(arr1, arr2).Should().BeTrue();
        MessageUtils.CompareObjects(arr1, arr3).Should().BeFalse();
    }

    [Fact]
    public void CompareObjects_ComparesArrays()
    {
        // Arrange
        var arr1 = new object[] { 1, "hello", true };
        var arr2 = new object[] { 1, "hello", true };
        var arr3 = new object[] { 1, "hello", false };
        
        // Act & Assert
        MessageUtils.CompareObjects(arr1, arr2).Should().BeTrue();
        MessageUtils.CompareObjects(arr1, arr3).Should().BeFalse();
    }

    [Fact]
    public void CompareObjects_ComparesDictionaries()
    {
        // Arrange
        var dict1 = new Dictionary<string, object?> { { "a", 1 }, { "b", "hello" } };
        var dict2 = new Dictionary<string, object?> { { "a", 1 }, { "b", "hello" } };
        var dict3 = new Dictionary<string, object?> { { "a", 1 }, { "b", "world" } };
        
        // Act & Assert
        MessageUtils.CompareObjects(dict1, dict2).Should().BeTrue();
        MessageUtils.CompareObjects(dict1, dict3).Should().BeFalse();
    }

    [Fact]
    public void CreateError_CreatesExceptionWithCode()
    {
        // Act
        var error = MessageUtils.CreateError("TEST_CODE", "Test message");
        
        // Assert
        error.Should().BeOfType<InvalidOperationException>();
        error.Message.Should().Be("Test message");
        error.Data["code"].Should().Be("TEST_CODE");
    }

    [Fact]
    public void DeepClone_ClonesPrimitives()
    {
        // Act & Assert
        MessageUtils.DeepClone(42).Should().Be(42);
        MessageUtils.DeepClone("hello").Should().Be("hello");
        MessageUtils.DeepClone(true).Should().Be(true);
        MessageUtils.DeepClone(null).Should().BeNull();
    }

    [Fact]
    public void DeepClone_ClonesByteArrays()
    {
        // Arrange
        var original = new byte[] { 1, 2, 3 };
        
        // Act
        var clone = MessageUtils.DeepClone(original) as byte[];
        
        // Assert
        clone.Should().NotBeSameAs(original);
        clone.Should().BeEquivalentTo(original);
    }
}
