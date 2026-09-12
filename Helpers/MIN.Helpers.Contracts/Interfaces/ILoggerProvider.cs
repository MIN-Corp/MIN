using MIN.Helpers.Contracts.Models;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Helpers.Contracts.Interfaces;

/// <summary>
/// Сервис по работе с логами
/// </summary>
public interface ILoggerProvider
{
    /// <summary>
    /// Событие на получение лога
    /// </summary>
    event EventHandler<LogItem>? OnLogReceived;

    /// <summary>
    /// Залогировать
    /// </summary>
    void Log(string message, LogLevel level = LogLevel.Information, Type? callerType = null, double? durationMs = null);

    /// <summary>
    /// Получить историю логов
    /// </summary>
    IEnumerable<LogItem> GetRecentLogHistory(int? page, int? pageSize);
}
