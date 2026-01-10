// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using FluentAssertions;
using NodeRed.Core.Utilities;
using Xunit;

namespace NodeRed.Tests.Utilities;

public class EncodeUtilsTests
{
    [Fact]
    public void EncodeObject_Null_ReturnsUndefined()
    {
        // Act
        var result = EncodeUtils.EncodeObject(null);
        
        // Assert
        result.Format.Should().Be("null");
        result.Msg.Should().Be("(undefined)");
    }

    [Fact]
    public void EncodeObject_String_ReturnsString()
    {
        // Act
        var result = EncodeUtils.EncodeObject("hello world");
        
        // Assert
        result.Format.Should().Be("string[11]");
        result.Msg.Should().Be("hello world");
    }

    [Fact]
    public void EncodeObject_LongString_Truncates()
    {
        // Arrange
        var longString = new string('a', 2000);
        
        // Act
        var result = EncodeUtils.EncodeObject(longString, 100);
        
        // Assert
        result.Format.Should().Be("string[2000]");
        result.Msg.Should().HaveLength(103); // 100 chars + "..."
        result.Msg.Should().EndWith("...");
    }

    [Fact]
    public void EncodeObject_Number_ReturnsNumber()
    {
        // Act
        var result = EncodeUtils.EncodeObject(42);
        
        // Assert
        result.Format.Should().Be("number");
        result.Msg.Should().Be("42");
    }

    [Fact]
    public void EncodeObject_Double_ReturnsNumber()
    {
        // Act
        var result = EncodeUtils.EncodeObject(3.14);
        
        // Assert
        result.Format.Should().Be("number");
        result.Msg.Should().Contain("3.14");
    }

    [Fact]
    public void EncodeObject_Boolean_ReturnsBoolean()
    {
        // Act
        var resultTrue = EncodeUtils.EncodeObject(true);
        var resultFalse = EncodeUtils.EncodeObject(false);
        
        // Assert
        resultTrue.Format.Should().Be("boolean");
        resultTrue.Msg.Should().Be("true");
        resultFalse.Format.Should().Be("boolean");
        resultFalse.Msg.Should().Be("false");
    }

    [Fact]
    public void EncodeObject_ByteArray_ReturnsBuffer()
    {
        // Arrange
        var buffer = new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f }; // "Hello"
        
        // Act
        var result = EncodeUtils.EncodeObject(buffer);
        
        // Assert
        result.Format.Should().Be("buffer[5]");
        result.Msg.Should().Be("48656c6c6f");
    }

    [Fact]
    public void EncodeObject_Array_ReturnsArray()
    {
        // Arrange
        var array = new object[] { 1, 2, 3 };
        
        // Act
        var result = EncodeUtils.EncodeObject(array);
        
        // Assert
        result.Format.Should().Be("array[3]");
    }

    [Fact]
    public void EncodeObject_Exception_ReturnsError()
    {
        // Arrange
        var ex = new InvalidOperationException("Test error");
        
        // Act
        var result = EncodeUtils.EncodeObject(ex);
        
        // Assert
        result.Format.Should().Be("error");
        result.Msg.Should().Contain("InvalidOperationException");
        result.Msg.Should().Contain("Test error");
    }

    [Fact]
    public void EncodeObject_Dictionary_ReturnsObject()
    {
        // Arrange
        var dict = new Dictionary<string, object?>
        {
            { "name", "test" },
            { "value", 42 }
        };
        
        // Act
        var result = EncodeUtils.EncodeObject(dict);
        
        // Assert
        result.Format.Should().Be("map");
    }

    [Fact]
    public void EncodeObject_HashSet_ReturnsSet()
    {
        // Arrange
        var set = new HashSet<int> { 1, 2, 3 };
        
        // Act
        var result = EncodeUtils.EncodeObject(set);
        
        // Assert
        result.Format.Should().Be("set[3]");
    }

    [Fact]
    public void EncodeObject_NaN_EncodesAsSpecialNumber()
    {
        // Act
        var result = EncodeUtils.EncodeObject(double.NaN);
        
        // Assert
        result.Format.Should().Be("number");
        result.Msg.Should().Contain("NaN");
    }

    [Fact]
    public void EncodeObject_Infinity_EncodesAsSpecialNumber()
    {
        // Act
        var resultPosInf = EncodeUtils.EncodeObject(double.PositiveInfinity);
        var resultNegInf = EncodeUtils.EncodeObject(double.NegativeInfinity);
        
        // Assert
        resultPosInf.Format.Should().Be("number");
        resultPosInf.Msg.Should().Contain("Infinity");
        resultNegInf.Format.Should().Be("number");
        resultNegInf.Msg.Should().Contain("-Infinity");
    }
}
