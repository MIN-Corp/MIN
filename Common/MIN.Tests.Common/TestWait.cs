using System.Diagnostics;

namespace MIN.Tests.Common;

/// <summary>
/// Ожидание асинхронных условий в тестах
/// </summary>
public static class TestWait
{
    /// <summary>
    /// Дождаться выполнения условия с таймаутом
    /// </summary>
    public static async Task UntilAsync(Func<bool> condition, int timeoutMs = 5000, int pollMs = 20)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.ElapsedMilliseconds > timeoutMs)
            {
                throw new TimeoutException($"Условие не выполнено за {timeoutMs} мс");
            }

            await Task.Delay(pollMs);
        }
    }
}
