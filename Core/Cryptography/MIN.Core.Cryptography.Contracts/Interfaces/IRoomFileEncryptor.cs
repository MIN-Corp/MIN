namespace MIN.Core.Cryptography.Contracts.Interfaces;

/// <summary>
/// Помощник в шифровании файлов комнаты
/// </summary>
public interface IRoomFileEncryptor
{
    /// <summary>
    /// Закодировать файл
    /// </summary>
    /// <param name="data">Информация для зашифровки</param>
    byte[] Protect(byte[] data);

    /// <summary>
    /// Раскодировать файл
    /// </summary>
    /// <param name="encryptedData">Информация для расшифровки</param>
    byte[] DecryptMessage(byte[] encryptedData);
}
