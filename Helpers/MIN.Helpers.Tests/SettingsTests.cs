using FluentAssertions;
using MIN.Helpers.Contracts.Interfaces.SettingsServices;
using MIN.Helpers.Contracts.Models;
using MIN.Helpers.Services;
using MIN.Tests.Common;
using Moq;
using Xunit;

namespace MIN.Helpers.Tests;

/// <summary>
/// Тесты на <see cref="SettingsProvider"/> и <see cref="FileSystemSettingsStorage"/>
/// </summary>
public class SettingsTests
{
    /// <summary>
    /// GetSettings кеширует результат загрузки
    /// </summary>
    [Fact]
    public void GetSettingsShouldCache()
    {
        // Arrange
        var storage = new Mock<ISettingsStorage>();
        storage.Setup(s => s.Load()).Returns(new Settings { DiscoveryPort = 1234 });
        ISettingsProvider provider = new SettingsProvider(storage.Object);

        // Act
        var first = provider.GetSettings();
        var second = provider.GetSettings();

        // Assert
        first.Should().BeSameAs(second);
        first.DiscoveryPort.Should().Be(1234);
        storage.Verify(s => s.Load(), Times.Once);
    }

    /// <summary>
    /// SaveSettings обновляет кеш, хранилище и уведомляет
    /// </summary>
    [Fact]
    public async Task SaveSettingsShouldUpdateCacheAndNotify()
    {
        // Arrange
        var storage = new Mock<ISettingsStorage>();
        storage.Setup(s => s.Load()).Returns(new Settings());
        var notified = false;
        var provider = new SettingsProvider(storage.Object) { OnSettingsSaved = () => notified = true };
        var newSettings = new Settings { DiscoveryPort = 4321 };

        // Act
        await ((ISettingsProvider)provider).SaveSettings(newSettings);

        // Assert
        ((ISettingsProvider)provider).GetSettings().Should().BeSameAs(newSettings);
        storage.Verify(s => s.Save(newSettings), Times.Once);
        notified.Should().BeTrue();
    }

    /// <summary>
    /// Файловое хранилище отдаёт дефолтные настройки при отсутствии файла
    /// </summary>
    [Fact]
    public void FileSystemStorageLoadMissingShouldReturnDefaults()
    {
        // Arrange
        using var appData = new TestAppDataProvider();
        ISettingsStorage storage = new FileSystemSettingsStorage(appData, new TestLoggerProvider());

        // Act
        var settings = storage.Load();

        // Assert
        settings.Should().NotBeNull();
        settings.DiscoveryPort.Should().Be(42069);
    }

    /// <summary>
    /// Настройки сохраняются и читаются обратно
    /// </summary>
    [Fact]
    public async Task FileSystemStorageShouldRoundTrip()
    {
        // Arrange
        using var appData = new TestAppDataProvider();
        ISettingsStorage storage = new FileSystemSettingsStorage(appData, new TestLoggerProvider());
        var settings = new Settings { DiscoveryPort = 5555 };

        // Act
        await storage.Save(settings);
        var loaded = storage.Load();

        // Assert
        loaded.DiscoveryPort.Should().Be(5555);
    }

    /// <summary>
    /// Повреждённый файл настроек сбрасывается с резервной копией
    /// </summary>
    [Fact]
    public async Task FileSystemStorageCorruptedShouldReset()
    {
        // Arrange
        using var appData = new TestAppDataProvider();
        ISettingsStorage storage = new FileSystemSettingsStorage(appData, new TestLoggerProvider());
        var settingsPath = Path.Combine(appData.SharedDirectory, "settings", "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{битый json");

        // Act
        var loaded = storage.Load();

        // Assert
        loaded.DiscoveryPort.Should().Be(42069);
        File.Exists(settingsPath + ".bak").Should().BeTrue();
    }
}
