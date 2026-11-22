namespace MIN.Core.Services.Contracts.Interfaces.Moderation;

/// <summary>
/// Сервис по обработке исключений на уровне сети
/// </summary>
public interface INetworkErrorHandler
{
    /// <summary>
    /// Отправить ошибку для клиента, указав причину
    /// </summary>
    /// <remarks>
    /// если critical = true - нужно разорвать соединение
    /// </remarks>
    Task SendErrorAsync(string message, Guid recipientId, Guid roomId, bool critical = false, int timeoutMs = 5000);

    /// <summary>
    /// Отправить ошибку для соединения, указав причину
    /// </summary>
    /// <remarks>
    /// Если critical = true - нужно разорвать соединение;
    /// kickParticipant = false - участник остаётся в комнате (только разрыв соединения)
    /// </remarks>
    Task SendErrorToConnectionAsync(string message, Guid connectionId, Guid roomId, bool critical = false, int timeoutMs = 5000, bool kickParticipant = true);
}
