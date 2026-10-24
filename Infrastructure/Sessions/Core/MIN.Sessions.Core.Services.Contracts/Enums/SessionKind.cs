namespace MIN.Sessions.Core.Services.Contracts.Enums;

/// <summary>
/// Вид сессии
/// </summary>
public enum SessionKind
{
    /// <summary>
    /// Обычная сессия во внешнем процессе
    /// </summary>
    Process,

    /// <summary>
    /// Туннель до открытого порта игры
    /// </summary>
    PortTunnel,
}
