using MIN.Core.Handlers.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Core.Handlers.Contracts.Base;

/// <summary>
/// Базовый обработчик сообщений, следящий за соответсвие типов и предоставляющий базовые сервисы (по типу логирования)
/// </summary>
public abstract class BaseHandler : IMessageHandler
{
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BaseHandler"/>
    /// </summary>
    protected BaseHandler(ILoggerProvider logger)
    {
        this.logger = logger;
        Priority = 0;
    }

    /// <inheritdoc />
    public abstract IEnumerable<MessageTypeTag> HandledTypes { get; }

    /// <inheritdoc />
    public virtual int Priority { get; }

    async Task<HandlerResult> IMessageHandler.HandleAsync(IMessage message, MessageContext context)
    {
        if (!HandledTypes.Contains(message.TypeTag))
        {
            LogWarning($"Неизвестный тип сообщения в {GetType().Name} - {message.GetType()}");
            return HandlerResult.Failure($"Неизвестный тип сообщения в {GetType().Name} - {message.GetType()}");
        }

        return await HandleAsync(message, context);
    }

    /// <summary>
    /// Залогировать информацию
    /// </summary>
    protected void LogInfo(string message) => logger.Log(message, LogLevel.Information, GetType());

    /// <summary>
    /// Залогировать предупреждение
    /// </summary>
    protected void LogWarning(string message) => logger.Log(message, LogLevel.Warning, GetType());

    /// <summary>
    /// Залогировать ошибку
    /// </summary>
    protected void LogError(string message) => logger.Log(message, LogLevel.Error, GetType());

    /// <summary>
    /// Абстрактный метод, который должны реализовать все обработчики
    /// </summary>
    protected abstract Task<HandlerResult> HandleAsync(IMessage message, MessageContext context);
}
