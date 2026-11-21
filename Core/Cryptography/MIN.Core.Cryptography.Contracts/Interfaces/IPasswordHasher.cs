namespace MIN.Core.Cryptography.Contracts.Interfaces;

/// <summary>
/// Хеширование и проверка паролей комнат
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Вычислить верификатор пароля для комнаты
    /// </summary>
    /// <remarks>
    /// Соль выводится из id комнаты; формат результата: "{итерации}:{hashBase64}"
    /// </remarks>
    string HashPassword(string password, Guid roomId);

    /// <summary>
    /// Проверить, что присланный верификатор совпадает с сохранённым
    /// </summary>
    bool VerifyVerifier(string? providedVerifier, string? storedVerifier);
}
