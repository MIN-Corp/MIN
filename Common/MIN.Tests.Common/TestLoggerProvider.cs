using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Tests.Common;

/// <summary>
/// Тестовый логгер, сохраняющий все записи в память
/// </summary>
public sealed class TestLoggerProvider : ILoggerProvider
{
    private readonly List<LogItem> items = [];
    private readonly object lockObj = new();

    /// <inheritdoc />
    public event EventHandler<LogItem>? OnLogReceived;

    /// <summary>
    /// Снимок всех записей лога
    /// </summary>
    public IReadOnlyList<LogItem> Items
    {
        get
        {
            lock (lockObj)
            {
                return items.ToList();
            }
        }
    }

    /// <summary>
    /// Есть ли среди записей ошибки
    /// </summary>
    public bool HasErrors => Items.Any(x => x.LogLevel == LogLevel.Error);

    /// <inheritdoc />
    public void Log(string message, LogLevel logLevel = LogLevel.Information, Type? type = null, double? elapsedMs = null)
    {
        var item = new LogItem(message, logLevel);
        lock (lockObj)
        {
            items.Add(item);
        }

        OnLogReceived?.Invoke(this, item);
    }

    /// <inheritdoc />
    public IEnumerable<LogItem> GetRecentLogHistory(int? page, int? pageSize)
    {
        lock (lockObj)
        {
            var query = items.AsEnumerable();
            if (page.HasValue && pageSize.HasValue)
            {
                query = query.Skip(page.Value * pageSize.Value).Take(pageSize.Value);
            }

            return query.ToList();
        }
    }
}
