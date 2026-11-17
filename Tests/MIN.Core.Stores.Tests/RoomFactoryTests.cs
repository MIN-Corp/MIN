using FluentAssertions;
using MIN.Core.Stores.Factories;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Stores.Tests;

/// <summary>
/// Тесты на <see cref="RoomFactory"/>
/// </summary>
public class RoomFactoryTests : IDisposable
{
    private readonly TestRoomFactory factory = new();

    /// <summary>
    /// GetOrCreateContext возвращает один и тот же контекст для одной комнаты
    /// </summary>
    [Fact]
    public void GetOrCreateContextShouldBeIdempotent()
    {
        // Arrange
        var roomId = Guid.NewGuid();

        // Act
        var first = factory.Factory.GetOrCreateContext(roomId);
        var second = factory.Factory.GetOrCreateContext(roomId);

        // Assert
        first.Should().BeSameAs(second);
        first.RoomId.Should().Be(roomId);
    }

    /// <summary>
    /// Разные комнаты получают разные контексты
    /// </summary>
    [Fact]
    public void DifferentRoomsShouldGetDifferentContexts()
    {
        // Act
        var first = factory.CreateContext();
        var second = factory.CreateContext();

        // Assert
        first.Should().NotBeSameAs(second);
        factory.Factory.GetAllContexts().Should().HaveCount(2);
    }

    /// <summary>
    /// TryGetContext различает существующие и неизвестные комнаты
    /// </summary>
    [Fact]
    public void TryGetContextShouldWork()
    {
        // Arrange
        var context = factory.CreateContext();

        // Act
        var found = factory.Factory.TryGetContext(context.RoomId, out var tried);
        var missing = factory.Factory.TryGetContext(Guid.NewGuid(), out _);

        // Assert
        found.Should().BeTrue();
        tried.Should().BeSameAs(context);
        missing.Should().BeFalse();
    }

    /// <summary>
    /// GetRoomContext для неизвестной комнаты возвращает null (сигнатура RoomContext?)
    /// </summary>
    [Fact]
    public void GetRoomContextUnknownShouldReturnNull()
    {
        // Act
        var context = factory.Factory.GetRoomContext(Guid.NewGuid());

        // Assert
        context.Should().BeNull();
    }

    /// <summary>
    /// DestroyContext удаляет контекст
    /// </summary>
    [Fact]
    public void DestroyContextShouldRemoveContext()
    {
        // Arrange
        var context = factory.CreateContext();

        // Act
        factory.Factory.DestroyContext(context.RoomId);

        // Assert
        factory.Factory.TryGetContext(context.RoomId, out _).Should().BeFalse();
        factory.Factory.GetAllContexts().Should().BeEmpty();
    }

    /// <inheritdoc />
    public void Dispose() => factory.Dispose();
}
