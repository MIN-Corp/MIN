using System.Diagnostics;
using System.Text;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Helpers.Services;

/// <inheritdoc cref="ILoggerProvider"/>
public class LoggerProvider : ILoggerProvider
{
    private readonly List<LogItem> messages = [];
    private readonly SortedDictionary<double, LogLevel> durationLevels = new()
    {
        { 0f,    LogLevel.Information },
        { 100f,  LogLevel.Warning },
        { 1000f, LogLevel.Error },
    };

    ///<inheritdoc cref="ILoggerProvider.OnLogReceived"/>
    public event EventHandler<LogItem>? OnLogReceived;

    void ILoggerProvider.Log(string message, LogLevel level, Type? callerType, double? durationMs)
    {
        string? callerClass;
        if (callerType == null)
        {
            var stackTrace = new StackTrace();
            var frame = stackTrace.GetFrame(1);
            callerClass = frame?.GetMethod()?.DeclaringType?.FullName ?? "Unknown";
        }
        else
        {
            callerClass = callerType.FullName ?? "Unknown";
        }

        if (durationMs != null)
        {
            level = GetLevelOutOfDuration(durationMs.Value);
        }

        var formatted = new StringBuilder();
        formatted.Append(DateTime.Now.ToString("HH:mm:ss.fff"));
        formatted.Append(" - ");
        formatted.Append(Enum.GetName(level));
        formatted.Append(" - [");
        formatted.Append(callerClass);
        formatted.Append("] ");
        formatted.Append(message);
        var result = formatted.ToString();

        var item = new LogItem(result, level);

        messages.Add(item);
        OnLogReceived?.Invoke(this, item);
    }

    private LogLevel GetLevelOutOfDuration(double duration)
    {
        LogLevel result = LogLevel.Information;

        foreach (var kvp in durationLevels)
        {
            if (duration >= kvp.Key)
            {
                result = kvp.Value;
            }
            else
            {
                break;
            }
        }

        return result;
    }

    IEnumerable<LogItem> ILoggerProvider.GetRecentLogHistory(int? page, int? pageSize)
    {
        if (page.HasValue && pageSize.HasValue)
        {
            return messages.AsEnumerable().Reverse().Skip(page.Value * pageSize.Value).Take(pageSize.Value);
        }
        else
        {
            return messages;
        }
    }
}
