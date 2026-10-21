namespace MIN.Core.Transport.Contracts.Enum;

/// <summary>
/// Откуда был получен адрес
/// </summary>
public enum AddressOrigin
{
    /// <summary>
    /// Из локальной сети
    /// </summary>
    Lan,

    /// <summary>
    /// Из VPN сервисов
    /// </summary>
    Vpn,

    /// <summary>
    /// Из проброски порта как публичная сеть
    /// </summary>
    Wan
}
