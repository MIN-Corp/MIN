using System.Diagnostics.CodeAnalysis;

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

    /// <summary>
    /// Равенство
    /// </summary>
    public override bool Equals([NotNullWhen(true)] object? obj) => base.Equals(obj);

    /// <summary>
    /// Равенство
    /// </summary>
    public static bool operator ==(NetworkOptions networkOptions, NetworkOptions networkOptions2)
        => networkOptions.PrefferredPort == networkOptions2.PrefferredPort
            || networkOptions.EnableLocalDiscovery == networkOptions2.EnableLocalDiscovery
            || networkOptions.EnablePortForwarding == networkOptions2.EnablePortForwarding
            || networkOptions.EnableRadmin == networkOptions2.EnableRadmin
            || networkOptions.EnableWeb == networkOptions2.EnableWeb;

    /// <summary>
    /// Неравенство
    /// </summary>
    public static bool operator !=(NetworkOptions networkOptions, NetworkOptions networkOptions2)
        => networkOptions.PrefferredPort != networkOptions2.PrefferredPort
            || networkOptions.EnableLocalDiscovery != networkOptions2.EnableLocalDiscovery
            || networkOptions.EnablePortForwarding != networkOptions2.EnablePortForwarding
            || networkOptions.EnableRadmin != networkOptions2.EnableRadmin
            || networkOptions.EnableWeb != networkOptions2.EnableWeb;

    /// <summary>
    /// Получить хеш-код
    /// </summary>
    public override int GetHashCode() => base.GetHashCode();
}
