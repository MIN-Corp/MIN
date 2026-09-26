using System;
using System.Threading;
using System.Threading.Tasks;
using MIN.Core.Entities;
using MIN.Core.Transport.Contracts.Interfaces;

namespace MIN.Desktop.Contracts.Models;

/// <summary>
/// Аргументы для присоединения к комнате
/// </summary>
public class RoomJoinArgs
{
    /// <summary>
    /// Адрес подключения
    /// </summary>
    public required IEndpoint Endpoint { get; init; }

    /// <summary>
    /// Ожидаемый идентификатор комнаты
    /// </summary>
    public Guid? ExpectedRoomId { get; init; }

    /// <summary>
    /// Токен отмены
    /// </summary>
    public required CancellationTokenSource Cts { get; init; }

    /// <summary>
    /// Событие по готовности комнаты
    /// </summary>
    public required Func<Room?, Guid, Task> OnRoomReady { get; init; }
}
