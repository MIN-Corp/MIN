namespace MIN.Helpers.Contracts.Constants;

/// <summary>
/// Глобальный флаг профилирования (включается через MIN_PROFILE=1 или --profile-startup)
/// </summary>
public static class Profiling
{
    /// <summary>
    /// Включено ли профилирование
    /// </summary>
    public readonly static bool IsEnabled =
#if DEBUG
        true;
#else
        Environment.GetEnvironmentVariable("MIN_PROFILE") == "1"
            || Environment.GetCommandLineArgs().Contains("--profile-startup");
#endif

}
