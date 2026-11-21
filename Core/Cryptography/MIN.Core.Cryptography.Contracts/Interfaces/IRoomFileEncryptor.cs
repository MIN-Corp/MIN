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
    /// <exception cref="System.IO.InvalidDataException">Данные короче IV + тега аутентичности</exception>
    /// <exception cref="System.Security.Cryptography.AuthenticationTagMismatchException">Данные повреждены или расшифрованы чужим ключом</exception>
    byte[] DecryptMessage(byte[] encryptedData);
}
