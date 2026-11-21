using System.Security.Cryptography;
using System.Text;
using MIN.Core.Cryptography.Contracts.Interfaces;

namespace MIN.Core.Cryptography;

/// <inheritdoc cref="IPasswordHasher"/>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int Iterations = 600_000;
    private const int HashSize = 32;

    /// <inheritdoc />
    public string HashPassword(string password, Guid roomId)
    {
        var salt = SHA256.HashData(roomId.ToByteArray());
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        return $"{Iterations}:{Convert.ToBase64String(hash)}";
    }

    /// <inheritdoc />
    public bool VerifyVerifier(string? providedVerifier, string? storedVerifier)
    {
        if (string.IsNullOrEmpty(providedVerifier) || string.IsNullOrEmpty(storedVerifier))
        {
            return false;
        }

        var provided = Encoding.UTF8.GetBytes(providedVerifier);
        var stored = Encoding.UTF8.GetBytes(storedVerifier);

        return provided.Length == stored.Length
               && CryptographicOperations.FixedTimeEquals(provided, stored);
    }
}
