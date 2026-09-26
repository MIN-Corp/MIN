namespace MIN.Core.Cryptography.Contracts.Models;

/// <summary>
/// Результат обмена ключами
/// </summary>
public sealed class KeyNegotiationResult
{
    /// <summary>
    /// Нужно получить публичный ключ
    /// </summary>
    public bool NeedFullKey { get; init; }

    /// <summary>
    /// Отправлен полный ключ
    /// </summary>
    public bool SendFullKey { get; init; }
}
