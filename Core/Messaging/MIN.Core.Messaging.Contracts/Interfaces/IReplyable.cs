namespace MIN.Core.Messaging.Contracts.Interfaces;

/// <summary>
/// Сообщение, которое может являться как ответом на другое сообщение
/// </summary>
/// <remarks>
/// Null - если это не ответ
/// </remarks>
public interface IReplyable
{
    /// <summary>
    /// Идентификатор сообщения, на которое дан ответ
    /// </summary>
    Guid? ReplyToMessageId { get; set; }

    /// <summary>
    /// Описание сообщения, на которое дан ответ
    /// </summary>
    string? ReplyToMessageDescription { get; set; }
}
