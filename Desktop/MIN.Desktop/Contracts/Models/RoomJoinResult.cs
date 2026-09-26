using System;
using MIN.Core.Services.Contracts.Exceptions;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Models.Enums;

namespace MIN.Desktop.Contracts.Models;

/// <summary>
/// Результат присоединения к комнате
/// </summary>
public class RoomJoinResult
{
    /// <summary>
    /// Ошибка подключения
    /// </summary>
    public JoinFailure? Failure { get; init; } = JoinFailure.Error;

    /// <summary>
    /// Выбор по смене комнате (если комната сменилась)
    /// </summary>
    public RoomMismatchChoice? RoomMismatchChoice { get; init; }

    /// <summary>
    /// Ошибка подключения из-за смены комнаты (если комната сменилась)
    /// </summary>
    public RoomIdentityMismatchException? RoomIdentityMismatchException { get; init; }

    /// <summary>
    /// Идентификатор комнаты
    /// </summary>
    public Guid RoomId { get; init; }

    /// <summary>
    /// Идентификатор соединения
    /// </summary>
    public Guid ConnectionId { get; init; }
}
