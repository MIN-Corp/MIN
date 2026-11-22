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
    public JoinFailure? Failure { get; init; }

    /// <summary>
    /// Выбор по смене комнате (если комната сменилась)
    /// </summary>
    public RoomMismatchChoice? RoomMismatchChoice { get; init; }

    /// <summary>
    /// Ошибка подключения из-за смены комнаты (если комната сменилась)
    /// </summary>
    public RoomIdentityMismatchException? RoomIdentityMismatchException { get; init; }

    /// <summary>
    /// Пароль был отклонён
    /// </summary>
    public bool PasswordRejected { get; init; }
}
