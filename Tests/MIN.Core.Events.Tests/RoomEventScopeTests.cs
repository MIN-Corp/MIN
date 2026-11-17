using FluentAssertions;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Services;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Events.Tests;

/// <summary>
/// Тесты на <see cref="RoomEventScope"/>
/// </summary>
public class RoomEventScopeTests
{
    /// <summary>
    /// Scope пропускает только события своей комнаты
    /// </summary>
    [Fact]
    public async Task ScopeShouldFilterByRoomId()
    {
        // Arrange
        await using var bus = new InMemoryEventBus(new TestLoggerProvider());
        var roomA = Guid.NewGuid();
        var roomB = Guid.NewGuid();
        using var scope = ((IEventBus)bus).CreateScope(roomA);
        var received = new List<ScopedTestEvent>();
        scope.Subscribe<ScopedTestEvent>((e, _) => { received.Add(e); return Task.CompletedTask; });

        // Act
        await ((IEventBus)bus).PublishAsync(new ScopedTestEvent { RoomId = roomA, Payload = "своя" }, CancellationToken.None);
        await ((IEventBus)bus).PublishAsync(new ScopedTestEvent { RoomId = roomB, Payload = "чужая" }, CancellationToken.None);

        // Assert
        received.Should().HaveCount(1);
        received[0].Payload.Should().Be("своя");
    }

    /// <summary>
    /// Освобождение scope отписывает все его подписки
    /// </summary>
    [Fact]
    public async Task ScopeDisposeShouldUnsubscribeAll()
    {
        // Arrange
        await using var bus = new InMemoryEventBus(new TestLoggerProvider());
        var roomId = Guid.NewGuid();
        var scope = ((IEventBus)bus).CreateScope(roomId);
        var count = 0;
        scope.Subscribe<ScopedTestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        scope.Subscribe<ScopedTestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        // Act
        scope.Dispose();
        await ((IEventBus)bus).PublishAsync(new ScopedTestEvent { RoomId = roomId }, CancellationToken.None);

        // Assert
        count.Should().Be(0);
    }

    /// <summary>
    /// Два scope разных комнат получают только свои события
    /// </summary>
    [Fact]
    public async Task MultipleScopesShouldIsolate()
    {
        // Arrange
        await using var bus = new InMemoryEventBus(new TestLoggerProvider());
        var roomA = Guid.NewGuid();
        var roomB = Guid.NewGuid();
        using var scopeA = ((IEventBus)bus).CreateScope(roomA);
        using var scopeB = ((IEventBus)bus).CreateScope(roomB);
        var countA = 0;
        var countB = 0;
        scopeA.Subscribe<ScopedTestEvent>((_, _) => { Interlocked.Increment(ref countA); return Task.CompletedTask; });
        scopeB.Subscribe<ScopedTestEvent>((_, _) => { Interlocked.Increment(ref countB); return Task.CompletedTask; });

        // Act
        await ((IEventBus)bus).PublishAsync(new ScopedTestEvent { RoomId = roomA }, CancellationToken.None);
        await ((IEventBus)bus).PublishAsync(new ScopedTestEvent { RoomId = roomB }, CancellationToken.None);
        await ((IEventBus)bus).PublishAsync(new ScopedTestEvent { RoomId = roomB }, CancellationToken.None);

        // Assert
        countA.Should().Be(1);
        countB.Should().Be(2);
    }
}
