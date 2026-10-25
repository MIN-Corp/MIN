using MIN.Sessions.Core.Messaging.Contracts.Enums;
using MIN.Sessions.Core.Messaging.Contracts.Models;

namespace MIN.Sessions.Core.Messaging.Ipc;

/// <inheritdoc cref="IpcMessageType.InSession"/>
public sealed record InSessionMessage(byte[] Body) : IpcMessage(IpcMessageType.InSession);
