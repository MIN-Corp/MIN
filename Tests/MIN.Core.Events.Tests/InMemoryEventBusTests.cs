using FluentAssertions;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Services;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Events.Tests;

/// <summary>
/// Тесты на <see cref="InMemoryEventBus"/>
/// </summary>
public class InMemoryEventBusTests
{
    private readonly TestLoggerProvider logger = new();

    /// <summary>
    /// Создать шину
    /// </summary>
    private IEventBus CreateBus() => new InMemoryEventBus(logger);

    /// <summary>
    /// Опубликованное событие доходит до подписчика с теми же данными
    /// </summary>
    [Fact]
    public async Task PublishShouldDeliverEventWithData()
    {
        // Arrange
        await using var bus = new InMemoryEventBus(logger);
        var published = new TestEvent { RoomId = Guid.NewGuid(), Payload = "нагрузка", Number = 42 };
        TestEvent? received = null;
        ((IEventBus)bus).Subscribe<TestEvent>((e, _) =>
        {
            received = e;
            return Task.CompletedTask;
        });

        // Act
        await ((IEventBus)bus).PublishAsync(published, CancellationToken.None);

        // Assert
        received.Should().NotBeNull();
        received.Should().BeSameAs(published);
        received!.RoomId.Should().Be(published.RoomId);
        received.Payload.Should().Be("нагрузка");
        received.Number.Should().Be(42);
    }

    /// <summary>
    /// Публикация без подписчиков завершается успешно
    /// </summary>
    [Fact]
    public async Task PublishWithoutSubscribersShouldComplete()
    {
        // Arrange
        await using var bus = new InMemoryEventBus(logger);

        // Act
        var act = () => ((IEventBus)bus).PublishAsync(new TestEvent(), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// Все подписчики получают событие
    /// </summary>
    [Fact]
    public async Task MultipleSubscribersShouldAllReceive()
    {
        // Arrange
        await using var bus = new InMemoryEventBus(logger);
        var count = 0;
        ((IEventBus)bus).Subscribe<TestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        ((IEventBus)bus).Subscribe<TestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        ((IEventBus)bus).Subscribe<TestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        // Act
        await ((IEventBus)bus).PublishAsync(new TestEvent(), CancellationToken.None);

        // Assert
        count.Should().Be(3);
    }

    /// <summary>
    /// Отписка через токен останавливает доставку
    /// </summary>
    [Fact]
    public async Task UnsubscribeShouldStopDelivery()
    {
        // Arrange
        await using var bus = new InMemoryEventBus(logger);
        var count = 0;
        var token = ((IEventBus)bus).Subscribe<TestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        await ((IEventBus)bus).PublishAsync(new TestEvent(), CancellationToken.None);

        // Act
        token.Dispose();
        await ((IEventBus)bus).PublishAsync(new TestEvent(), CancellationToken.None);

        // Assert
        count.Should().Be(1);
    }

    /// <summary>
    /// Повторное освобождение токена безопасно
    /// </summary>
    [Fact]
    public async Task DisposeTokenTwiceShouldBeSafe()
    {
        // Arrange
        await using var bus = new InMemoryEventBus(logger);
        var count = 0;
        var token = ((IEventBus)bus).Subscribe<TestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        // Act
        token.Dispose();
        token.Dispose();
        await ((IEventBus)bus).PublishAsync(new TestEvent(), CancellationToken.None);

        // Assert
        count.Should().Be(0);
    }

    /// <summary>
    /// Исключение в одном обработчике не мешает другим и логируется
    /// </summary>
    [Fact]
    public async Task HandlerExceptionShouldNotBreakPublish()
    {
        // Arrange
        await using var bus = new InMemoryEventBus(logger);
        var secondReceived = false;
        ((IEventBus)bus).Subscribe<TestEvent>((_, _) => throw new InvalidOperationException("бум"));
        ((IEventBus)bus).Subscribe<TestEvent>((_, _) => { secondReceived = true; return Task.CompletedTask; });

        // Act
        await ((IEventBus)bus).PublishAsync(new TestEvent(), CancellationToken.None);

        // Assert
        secondReceived.Should().BeTrue();
        logger.HasErrors.Should().BeTrue();
    }

    /// <summary>
    /// Обработчики выполняются параллельно, а не по очереди
    /// </summary>
    [Fact]
    public async Task PublishShouldRunHandlersConcurrently()
    {
        // Arrange
        await using var bus = new InMemoryEventBus(logger);
        var entered = 0;
        var gate = new TaskCompletionSource();
        ((IEventBus)bus).Subscribe<TestEvent>(async (_, _) => { Interlocked.Increment(ref entered); await gate.Task; });
        ((IEventBus)bus).Subscribe<TestEvent>(async (_, _) => { Interlocked.Increment(ref entered); await gate.Task; });

        // Act
        var publish = ((IEventBus)bus).PublishAsync(new TestEvent(), CancellationToken.None);
        await TestWait.UntilAsync(() => Volatile.Read(ref entered) == 2, timeoutMs: 2000);
        gate.SetResult();
        await publish;

        // Assert
        entered.Should().Be(2);
    }

    /// <summary>
    /// Отменённый токен доходит до обработчика
    /// </summary>
    [Fact]
    public async Task CancelledTokenShouldReachHandler()
    {
        // Arrange
        await using var bus = new InMemoryEventBus(logger);
        using var cts = new CancellationTokenSource();
        CancellationToken observed = default;
        ((IEventBus)bus).Subscribe<TestEvent>((_, ct) => { observed = ct; return Task.CompletedTask; });
        cts.Cancel();

        // Act
        await ((IEventBus)bus).PublishAsync(new TestEvent(), cts.Token);

        // Assert
        observed.IsCancellationRequested.Should().BeTrue();
    }

    /// <summary>
    /// Освобождение шины отменяет выполняющиеся обработчики
    /// </summary>
    [Fact]
    public async Task DisposeAsyncShouldCancelInFlightHandlers()
    {
        // Arrange
        var bus = new InMemoryEventBus(logger);
        var started = new TaskCompletionSource();
        var cancelled = new TaskCompletionSource();
        ((IEventBus)bus).Subscribe<TestEvent>(async (_, ct) =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                cancelled.SetResult();
            }
        });

        // Act
        var publish = ((IEventBus)bus).PublishAsync(new TestEvent(), CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await bus.DisposeAsync();

        // Assert
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await publish.WaitAsync(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Публикация после освобождения шины безопасна и ничего не доставляет
    /// </summary>
    [Fact]
    public async Task PublishAfterDisposeShouldBeNoOp()
    {
        // Arrange
        var bus = new InMemoryEventBus(logger);
        var count = 0;
        ((IEventBus)bus).Subscribe<TestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        await bus.DisposeAsync();

        // Act
        var act = () => ((IEventBus)bus).PublishAsync(new TestEvent(), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        count.Should().Be(0);
    }

    /// <summary>
    /// События разных типов изолированы
    /// </summary>
    [Fact]
    public async Task DifferentEventTypesShouldBeIsolated()
    {
        // Arrange
        await using var bus = new InMemoryEventBus(logger);
        var otherReceived = false;
        ((IEventBus)bus).Subscribe<OtherTestEvent>((_, _) => { otherReceived = true; return Task.CompletedTask; });

        // Act
        await ((IEventBus)bus).PublishAsync(new TestEvent(), CancellationToken.None);

        // Assert
        otherReceived.Should().BeFalse();
    }

    /// <summary>
    /// Подписка на базовый тип не ловит наследников (точное совпадение типа)
    /// </summary>
    [Fact]
    public async Task DerivedEventShouldNotTriggerBaseHandler()
    {
        // Arrange
        await using var bus = new InMemoryEventBus(logger);
        var baseReceived = false;
        ((IEventBus)bus).Subscribe<TestEvent>((_, _) => { baseReceived = true; return Task.CompletedTask; });

        // Act
        await ((IEventBus)bus).PublishAsync(new DerivedTestEvent(), CancellationToken.None);

        // Assert
        baseReceived.Should().BeFalse();
    }

    /// <summary>
    /// 10 тысяч публикаций выполняются за разумное время
    /// </summary>
    [Fact]
    public async Task PublishPerformanceShouldBeReasonable()
    {
        // Arrange
        await using var bus = new InMemoryEventBus(logger);
        ((IEventBus)bus).Subscribe<TestEvent>((_, _) => Task.CompletedTask);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (var i = 0; i < 10_000; i++)
        {
            await ((IEventBus)bus).PublishAsync(new TestEvent(), CancellationToken.None);
        }

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "шина событий не должна быть узким местом");
    }
}
