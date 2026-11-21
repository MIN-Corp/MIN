using FluentAssertions;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Services;
using Xunit;

namespace MIN.Helpers.Tests;

/// <summary>
/// Тесты на <see cref="VersionProvider"/>
/// </summary>
public class VersionProviderTests
{
    /// <summary>
    /// Совпадающая версия совместима, отличная — нет
    /// </summary>
    [Fact]
    public void IsVersionCompatibleShouldCompareExact()
    {
        // Arrange
        IVersionProvider provider = new VersionProvider(new Version(2, 2, 0, 0));

        // Act & Assert
        provider.IsVersionCompatible(new Version(2, 2, 0, 0)).Should().BeTrue();
        provider.IsVersionCompatible(new Version(2, 1, 0, 0)).Should().BeFalse();
        provider.Version.Should().Be(new Version(2, 2, 0, 0));
    }
}
