using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using MIN.Helpers.Contracts.Interfaces;

namespace MIN.Desktop.Infrastructure.Diagnostics;

/// <summary>
/// Замеряет время первой резолюции (конструктор + транзитивные зависимости) всех singleton-сервисов
/// </summary>
public static class StartupProfiler
{
    /// <summary>
    /// Замерить время
    /// </summary>
    public static void Run(IServiceCollection services, IServiceProvider provider, ILoggerProvider logger)
    {
        var results = new List<(string Service, double Ms)>();

        foreach (var serviceType in services
                     .Select(d => d.ServiceType)
                     .Where(t => t is not { IsGenericType: true, IsConstructedGenericType: false })
                     .Distinct())
        {
            try
            {
                var sw = Stopwatch.StartNew();
                provider.GetService(serviceType);
                sw.Stop();
                results.Add((serviceType.Name, sw.Elapsed.TotalMilliseconds));
            }
            catch (Exception ex)
            {
                results.Add(($"{serviceType.Name} (ошибка: {ex.Message})", 0));
            }
        }

        foreach (var (service, ms) in results.OrderByDescending(r => r.Ms))
        {
            logger.Log($"[PROFILE] resolve {service} = {ms:F1} ms (thread {Environment.CurrentManagedThreadId})");
        }

        logger.Log($"[PROFILE] total warm resolution = {results.Sum(r => r.Ms):F1} ms");
    }
}
