// Copyright OpenJS Foundation and other contributors
// Licensed under the Apache License, Version 2.0

using FluentAssertions;
using NodeRed.Core.Interfaces;
using NodeRed.Runtime.Services;
using Xunit;

namespace NodeRed.Tests.Utilities;

public class HooksServiceTests
{
    private HooksService CreateService() => new HooksService();

    [Fact]
    public void Add_RegistersHook()
    {
        // Arrange
        var service = CreateService();
        var called = false;
        
        // Act
        service.Add("onSend.test", (payload) => { called = true; return true; });
        
        // Assert
        service.Has("onSend").Should().BeTrue();
        service.Has("onSend.test").Should().BeTrue();
    }

    [Fact]
    public void Add_ThrowsOnInvalidHook()
    {
        // Arrange
        var service = CreateService();
        
        // Act
        var act = () => service.Add("invalidHook", (_) => true);
        
        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*Invalid hook*");
    }

    [Fact]
    public void Add_ThrowsOnDuplicateLabelledHook()
    {
        // Arrange
        var service = CreateService();
        service.Add("onSend.test", (_) => true);
        
        // Act
        var act = () => service.Add("onSend.test", (_) => true);
        
        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*already registered*");
    }

    [Fact]
    public void Remove_RemovesHook()
    {
        // Arrange
        var service = CreateService();
        service.Add("onSend.test", (_) => true);
        
        // Act
        service.Remove("onSend.test");
        
        // Assert
        service.Has("onSend.test").Should().BeFalse();
    }

    [Fact]
    public void Remove_ThrowsWithoutLabel()
    {
        // Arrange
        var service = CreateService();
        
        // Act
        var act = () => service.Remove("onSend");
        
        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*Cannot remove hook without label*");
    }

    [Fact]
    public void Remove_WildcardRemovesAllForLabel()
    {
        // Arrange
        var service = CreateService();
        service.Add("onSend.test", (_) => true);
        service.Add("preRoute.test", (_) => true);
        
        // Act
        service.Remove("*.test");
        
        // Assert
        service.Has("onSend.test").Should().BeFalse();
        service.Has("preRoute.test").Should().BeFalse();
    }

    [Fact]
    public async Task TriggerAsync_CallsHooks()
    {
        // Arrange
        var service = CreateService();
        var callCount = 0;
        service.Add("onSend.test", (_) => { callCount++; return true; });
        
        // Act
        var result = await service.TriggerAsync("onSend", new object());
        
        // Assert
        result.Should().BeTrue();
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task TriggerAsync_CallsMultipleHooksInOrder()
    {
        // Arrange
        var service = CreateService();
        var order = new List<int>();
        service.Add("onSend.first", (_) => { order.Add(1); return true; });
        service.Add("onSend.second", (_) => { order.Add(2); return true; });
        service.Add("onSend.third", (_) => { order.Add(3); return true; });
        
        // Act
        await service.TriggerAsync("onSend", new object());
        
        // Assert
        order.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task TriggerAsync_HaltsOnFalseReturn()
    {
        // Arrange
        var service = CreateService();
        var callCount = 0;
        service.Add("onSend.first", (_) => { callCount++; return false; }); // Halt
        service.Add("onSend.second", (_) => { callCount++; return true; });
        
        // Act
        var result = await service.TriggerAsync("onSend", new object());
        
        // Assert
        result.Should().BeFalse();
        callCount.Should().Be(1); // Second hook not called
    }

    [Fact]
    public async Task TriggerAsync_ReturnsTrue_WhenNoHooks()
    {
        // Arrange
        var service = CreateService();
        
        // Act
        var result = await service.TriggerAsync("onSend", new object());
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TriggerAsync_WorksWithAsyncCallbacks()
    {
        // Arrange
        var service = CreateService();
        var called = false;
        service.Add("onSend.test", async (payload) =>
        {
            await Task.Delay(10);
            called = true;
            return true;
        });
        
        // Act
        await service.TriggerAsync("onSend", new object());
        
        // Assert
        called.Should().BeTrue();
    }

    [Fact]
    public void Has_ReturnsFalse_WhenNoHooks()
    {
        // Arrange
        var service = CreateService();
        
        // Assert
        service.Has("onSend").Should().BeFalse();
        service.Has("onSend.test").Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAllHooks()
    {
        // Arrange
        var service = CreateService();
        service.Add("onSend.test1", (_) => true);
        service.Add("preRoute.test2", (_) => true);
        
        // Act
        service.Clear();
        
        // Assert
        service.Has("onSend").Should().BeFalse();
        service.Has("preRoute").Should().BeFalse();
    }

    [Fact]
    public async Task TriggerAsync_SkipsRemovedHooks()
    {
        // Arrange
        var service = CreateService();
        var callCount = 0;
        service.Add("onSend.test", (_) => { callCount++; return true; });
        
        // Remove before trigger
        service.Remove("onSend.test");
        
        // Act
        await service.TriggerAsync("onSend", new object());
        
        // Assert
        callCount.Should().Be(0);
    }

    [Fact]
    public void ValidHooks_ContainsExpectedHooks()
    {
        // Assert - Message routing hooks
        IHooksService.MessageHooks.Should().Contain("onSend");
        IHooksService.MessageHooks.Should().Contain("preRoute");
        IHooksService.MessageHooks.Should().Contain("preDeliver");
        IHooksService.MessageHooks.Should().Contain("postDeliver");
        IHooksService.MessageHooks.Should().Contain("onReceive");
        IHooksService.MessageHooks.Should().Contain("postReceive");
        IHooksService.MessageHooks.Should().Contain("onComplete");
        
        // Module install hooks
        IHooksService.ModuleHooks.Should().Contain("preInstall");
        IHooksService.ModuleHooks.Should().Contain("postInstall");
        IHooksService.ModuleHooks.Should().Contain("preUninstall");
        IHooksService.ModuleHooks.Should().Contain("postUninstall");
    }
}
