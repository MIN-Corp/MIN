namespace MIN.Core.Identity.Contracts.Constants
{
    /// <summary>
    /// Константы для Identity
    /// </summary>
    public class IdentityConstants
    {
        /// <summary>
        /// Хеш-секрет для хеширования идентификатора пользователя
        /// </summary>
        public readonly static byte[] HashSecret =
            Convert.FromHexString("A4C9A124E6B12C4E6F81F3C54B2E6D8F6C18B3E5D70A9C7F8D0AF091B7E90D2A");
    }
}
