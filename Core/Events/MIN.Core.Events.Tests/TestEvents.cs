using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Models;

namespace MIN.Core.Events.Tests;

/// <summary>
/// Тестовое событие с полезной нагрузкой
/// </summary>
public record TestEvent : BaseEvent
{
    /// <summary>
    /// Идентификатор комнаты
    /// </summary>
    public Guid RoomId { get; init; }

    /// <summary>
    /// Текстовая нагрузка
    /// </summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>
    /// Числовая нагрузка
    /// </summary>
    public int Number { get; init; }
}

/// <summary>
/// Другой тип тестового события
/// </summary>
public sealed record OtherTestEvent : BaseEvent;

/// <summary>
/// Наследник тестового события
/// </summary>
public sealed record DerivedTestEvent : TestEvent;

/// <summary>
/// Тестовое событие с привязкой к комнате
/// </summary>
public sealed record ScopedTestEvent : BaseEvent, IRoomScopedEvent
{
    /// <inheritdoc />
    public Guid RoomId { get; init; }

    /// <summary>
    /// Текстовая нагрузка
    /// </summary>
    public string Payload { get; init; } = string.Empty;
}
