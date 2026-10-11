using MIN.Core.Events.Contracts.Models;
using MIN.Core.Messaging.Contracts.Interfaces;

namespace MIN.Core.Handlers.Contracts.Models;

/// <summary>
/// Результат обработки сообщения
/// </summary>
public sealed class HandlerResult
{
    /// <summary>
    /// Успешность обработки
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Флаг, указывающий, следует ли остановить дальнейшую обработку (для цепочек)
    /// </summary>
    public bool StopPropagation { get; init; }

    /// <summary>
    /// Флаг, указывающий, настолько ли критична ли ошибка, чтобы оставатьсяя подключённым
    /// </summary>
    public bool CriticalErrorForConnection { get; init; }

    /// <summary>
    /// Флаг, указывающий, настолько ли критична ли ошибка, чтобы сохранить комнату
    /// </summary>
    public bool CriticalErrorForDestroy { get; init; }

    /// <summary>
    /// Флаг, указывающий, надо ли публиковать ErrorOccurredEvent, или обработчик сам обработает
    /// </summary>
    public bool ShowErrorMessage { get; init; }

    /// <summary>
    /// Ответное сообщение (если требуется)
    /// </summary>
    public IMessage? Response { get; init; }

    /// <summary>
    /// Вызываемое событие (если требуется)
    /// </summary>
    public BaseEvent? ResultEvent { get; init; }

    /// <summary>
    /// Сообщение об ошибке (если не успешно)
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Создаёт успешный результат
    /// </summary>
    public static HandlerResult Success(bool stopPropagation = false)
        => new()
        {
            IsSuccess = true,
            StopPropagation = stopPropagation
        };

    /// <summary>
    /// Создаёт результат с ошибкой
    /// </summary>
    public static HandlerResult Failure(string errorMessage, bool stopPropagation = true, bool showErrorMessage = true, bool critical = false, bool destroy = false)
        => new()
        {
            IsSuccess = false,
            StopPropagation = stopPropagation,
            ShowErrorMessage = showErrorMessage,
            ErrorMessage = errorMessage,
            CriticalErrorForConnection = critical,
            CriticalErrorForDestroy = destroy
        };

    /// <summary>
    /// Создаёт результат с ответным сообщением
    /// </summary>
    public static HandlerResult WithResponse(IMessage response, bool stopPropagation = false)
        => new()
        {
            IsSuccess = true,
            StopPropagation = stopPropagation,
            Response = response
        };

    /// <summary>
    /// Создаёт результат с вызыванием события
    /// </summary>
    public static HandlerResult WithEvent(BaseEvent resultEvent, bool stopPropagation = false)
        => new()
        {
            IsSuccess = true,
            StopPropagation = stopPropagation,
            ResultEvent = resultEvent
        };

    /// <summary>
    /// Создаёт результат с возвратом ошибки отправителю
    /// </summary>
    public static HandlerResult WithErrorHandled(string errorMessage, bool stopPropagation = false, bool critical = false, bool destroy = false)
        => new()
        {
            IsSuccess = true,
            StopPropagation = stopPropagation,
            ErrorMessage = errorMessage,
            CriticalErrorForConnection = critical,
            CriticalErrorForDestroy = destroy,
        };
}
