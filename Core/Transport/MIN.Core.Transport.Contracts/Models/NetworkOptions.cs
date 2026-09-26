namespace MIN.Core.Transport.Contracts.Models;

/// <summary>
/// Настройки глобальности сети
/// </summary>
public struct NetworkOptions()
{
    /// <summary>
    /// Желаемый порт
    /// </summary>
    public ushort PrefferredPort { get; set; } = 5550;

    /// <summary>
    /// Локальное обнаружение
    /// </summary>
    public bool EnableLocalDiscovery { get; set; }

    /// <summary>
    /// Проброска порта
    /// </summary>
    public bool EnablePortForwarding { get; set; }

    /// <summary>
    /// Radmin
    /// </summary>
    public bool EnableRadmin { get; set; }

    /// <summary>
    /// Публикация в web
    /// </summary>
    public bool EnableWeb { get; set; }
}
