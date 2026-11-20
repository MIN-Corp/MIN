using System.Text.Json;
using FluentAssertions;
using MIN.Helpers.Contracts.Helpers;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Stores.Tests;

/// <summary>
/// Тесты на <see cref="SchemaFileStore"/>
/// </summary>
public class SchemaFileStoreTests : IDisposable
{
    private readonly TemporaryDirectory directory = new();
    private readonly TestLoggerProvider logger = new();
    private readonly SchemaFileStore store;
    private readonly string filePath;

    /// <summary>
    /// Тестовые данные
    /// </summary>
    private sealed class TestData
    {
        /// <summary>
        /// Значение
        /// </summary>
        public string Value { get; init; } = string.Empty;
    }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SchemaFileStoreTests"/>
    /// </summary>
    public SchemaFileStoreTests()
    {
        store = new SchemaFileStore(3, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }, logger);
        filePath = Path.Combine(directory.Path, "data.json");
    }

    /// <summary>
    /// Отсутствующий файл создаётся фабрикой
    /// </summary>
    [Fact]
    public void LoadOrCreateMissingShouldUseFactory()
    {
        // Act
        var data = store.LoadOrCreate(filePath, () => new TestData { Value = "новая" });

        // Assert
        data.Value.Should().Be("новая");
        File.Exists(filePath).Should().BeFalse();
    }

    /// <summary>
    /// Сохранение и чтение возвращают те же данные
    /// </summary>
    [Fact]
    public async Task SaveLoadShouldRoundTrip()
    {
        // Act
        await store.SaveAsync(filePath, new TestData { Value = "сохранённая" });
        var loaded = await store.LoadOrCreateAsync(filePath, () => new TestData { Value = "fallback" });

        // Assert
        loaded.Value.Should().Be("сохранённая");
        File.Exists(filePath + ".tmp").Should().BeFalse();
    }

    /// <summary>
    /// Повреждённый JSON откладывается в .bak и сбрасывается фабрикой
    /// </summary>
    [Fact]
    public async Task CorruptedJsonShouldBackupAndReset()
    {
        // Arrange
        await File.WriteAllTextAsync(filePath, "{битый json");

        // Act
        var loaded = await store.LoadOrCreateAsync(filePath, () => new TestData { Value = "fallback" });

        // Assert
        loaded.Value.Should().Be("fallback");
        File.Exists(filePath + ".bak").Should().BeTrue();
    }

    /// <summary>
    /// Файл без версии схемы (старый формат) откладывается в .bak
    /// </summary>
    [Fact]
    public async Task LegacyFormatWithoutVersionShouldBackupAndReset()
    {
        // Arrange
        await File.WriteAllTextAsync(filePath, """{"value":"старый формат"}""");

        // Act
        var loaded = await store.LoadOrCreateAsync(filePath, () => new TestData { Value = "fallback" });

        // Assert
        loaded.Value.Should().Be("fallback");
        File.Exists(filePath + ".bak").Should().BeTrue();
    }

    /// <summary>
    /// Файл новее текущей схемы откладывается в .bak
    /// </summary>
    [Fact]
    public async Task NewerSchemaVersionShouldBackupAndReset()
    {
        // Arrange
        await File.WriteAllTextAsync(filePath, """{"schemaVersion":99,"data":{"value":"из будущего"}}""");

        // Act
        var loaded = await store.LoadOrCreateAsync(filePath, () => new TestData { Value = "fallback" });

        // Assert
        loaded.Value.Should().Be("fallback");
        File.Exists(filePath + ".bak").Should().BeTrue();
    }

    /// <summary>
    /// Устаревшая схема мигрируется по шагам и пересохраняется
    /// </summary>
    [Fact]
    public async Task OlderSchemaVersionShouldMigrateStepByStep()
    {
        // Arrange
        await File.WriteAllTextAsync(filePath, """{"schemaVersion":1,"data":{"value":"старая"}}""");

        // Act
        var loaded = await store.LoadOrCreateAsync(
            filePath,
            () => new TestData { Value = "fallback" },
            (data, from) => new TestData { Value = data.Value + $"+{from}" });

        // Assert
        loaded.Value.Should().Be("старая+1+2");
        var reread = await store.LoadOrCreateAsync(filePath, () => new TestData { Value = "fallback" });
        reread.Value.Should().Be("старая+1+2");
        File.Exists(filePath + ".bak").Should().BeFalse();
    }

    /// <summary>
    /// Синхронные Save/Load работают так же
    /// </summary>
    [Fact]
    public void SyncSaveLoadShouldRoundTrip()
    {
        // Act
        store.Save(filePath, new TestData { Value = "синхронная" });
        var loaded = store.LoadOrCreate(filePath, () => new TestData { Value = "fallback" });

        // Assert
        loaded.Value.Should().Be("синхронная");
    }

    /// <summary>
    /// Повторное сохранение перезаписывает файл атомарно
    /// </summary>
    [Fact]
    public async Task RepeatedSaveShouldOverwrite()
    {
        // Act
        await store.SaveAsync(filePath, new TestData { Value = "первая" });
        await store.SaveAsync(filePath, new TestData { Value = "вторая" });
        var loaded = await store.LoadOrCreateAsync(filePath, () => new TestData { Value = "fallback" });

        // Assert
        loaded.Value.Should().Be("вторая");
        Directory.GetFiles(directory.Path).Should().HaveCount(1);
    }

    /// <inheritdoc />
    public void Dispose() => directory.Dispose();
}
