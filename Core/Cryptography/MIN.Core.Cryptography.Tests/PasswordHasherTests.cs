using FluentAssertions;
using Xunit;

namespace MIN.Core.Cryptography.Tests;

/// <summary>
/// Тесты на <see cref="PasswordHasher"/>
/// </summary>
public class PasswordHasherTests
{
    private readonly PasswordHasher hasher = new();

    /// <summary>
    /// Верификатор детерминирован и имеет формат "итерации:hashBase64"
    /// </summary>
    [Fact]
    public void HashPasswordShouldReturnStableFormattedVerifier()
    {
        // Arrange
        var roomId = Guid.NewGuid();

        // Act
        var first = hasher.HashPassword("пароль123", roomId);
        var second = hasher.HashPassword("пароль123", roomId);

        // Assert
        first.Should().Be(second);
        first.Should().MatchRegex(@"^\d+:[A-Za-z0-9+/]+={0,2}$");
    }

    /// <summary>
    /// Верификаторы для разных комнат различаются
    /// </summary>
    [Fact]
    public void HashPasswordShouldDifferAcrossRooms()
    {
        // Act
        var first = hasher.HashPassword("пароль123", Guid.NewGuid());
        var second = hasher.HashPassword("пароль123", Guid.NewGuid());

        // Assert
        first.Should().NotBe(second);
    }

    /// <summary>
    /// Верификаторы для разных паролей различаются
    /// </summary>
    [Fact]
    public void HashPasswordShouldDifferAcrossPasswords()
    {
        // Arrange
        var roomId = Guid.NewGuid();

        // Act
        var first = hasher.HashPassword("пароль123", roomId);
        var second = hasher.HashPassword("другой", roomId);

        // Assert
        first.Should().NotBe(second);
    }

    /// <summary>
    /// Совпадающий верификатор принимается
    /// </summary>
    [Fact]
    public void VerifyVerifierShouldAcceptMatchingVerifier()
    {
        // Arrange
        var stored = hasher.HashPassword("пароль123", Guid.NewGuid());

        // Act
        var result = hasher.VerifyVerifier(stored, stored);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// Чужой верификатор отклоняется
    /// </summary>
    [Fact]
    public void VerifyVerifierShouldRejectDifferentVerifier()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var stored = hasher.HashPassword("пароль123", roomId);

        // Act
        var result = hasher.VerifyVerifier(hasher.HashPassword("другой", roomId), stored);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Null и пустые верификаторы отклоняются
    /// </summary>
    [Theory]
    [InlineData(null, "600000:AbCd")]
    [InlineData("", "600000:AbCd")]
    [InlineData("600000:AbCd", null)]
    [InlineData(null, null)]
    public void VerifyVerifierShouldRejectNullOrEmpty(string? provided, string? stored)
    {
        // Act
        var result = hasher.VerifyVerifier(provided, stored);

        // Assert
        result.Should().BeFalse();
    }
}
