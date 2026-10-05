using System.Text.Json;
using MIN.Helpers.Contracts.Helpers;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces.SettingsServices;
using MIN.Helpers.Contracts.Models;

namespace MIN.Helpers.Services;

/// <inheritdoc cref="ISettingsStorage"/>
public sealed class FileSystemSettingsStorage : ISettingsStorage
{
    private readonly string settingsPath;
    private readonly SchemaFileStore schemaFileStore;
    private readonly JsonSerializerOptions jsonOptions;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="FileSystemSettingsStorage"/>
    /// </summary>
    public FileSystemSettingsStorage(IAppDataProvider appDataProvider, ILoggerProvider logger)
    {
        var directory = Directory.CreateDirectory(Path.Combine(appDataProvider.SharedDirectory, "settings")).FullName;
        settingsPath = Path.Combine(directory, "settings.json");
        jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        schemaFileStore = new(1, jsonOptions, logger);
    }

    Settings ISettingsStorage.Load()
        => schemaFileStore.LoadOrCreate(settingsPath, () => new Settings());

    Task ISettingsStorage.Save(Settings settings)
        => schemaFileStore.SaveAsync(settingsPath, settings);
}
