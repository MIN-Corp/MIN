using System.Text.Json;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Helpers.Contracts.Helpers;

/// <summary>
/// Управляет чтением/записью файлов с учётом версии схемы:
/// сравнивает версию, мигрирует устаревшие данные и делает резервную копию
/// (.bak) при повреждении или несовместимом формате. Потокобезопасность
/// доступа к конкретному файлу обеспечивает вызывающий.
/// </summary>
public sealed class SchemaFileStore
{
    private readonly int currentVersion;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ILoggerProvider logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SchemaFileStore"/>
    /// </summary>
    public SchemaFileStore(int currentVersion, JsonSerializerOptions jsonOptions, ILoggerProvider logger)
    {
        this.currentVersion = currentVersion;
        this.jsonOptions = jsonOptions;
        this.logger = logger;
    }

    /// <summary>
    /// Подгрузить или создать новый файл
    /// </summary>
    public T LoadOrCreate<T>(string path, Func<T> factory, Func<T, int, T>? migrate = null)
    {
        if (!File.Exists(path))
        {
            return factory();
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            logger?.Log($"Не удалось прочитать '{path}': {ex.Message}", LogLevel.Warning);
            return factory();
        }

        SchemaEnvelope<T>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<SchemaEnvelope<T>>(json, jsonOptions);
        }
        catch (JsonException ex)
        {
            logger?.Log($"Файл '{path}' повреждён: {ex.Message}. Резервная копия создана, данные сброшены.", LogLevel.Warning);
            Backup(path);
            return factory();
        }

        // SchemaVersion == 0 => старый/чужой формат без конверта (делаем бэкап).
        if (envelope is null || envelope.SchemaVersion == 0)
        {
            logger?.Log($"Файл '{path}' имеет устаревший формат (нет версии схемы). Резервная копия создана, данные сброшены.", LogLevel.Warning);
            Backup(path);
            return factory();
        }

        var data = envelope.Data ?? factory();

        if (envelope.SchemaVersion > currentVersion)
        {
            logger?.Log($"Файл '{path}' (v{envelope.SchemaVersion}) новее текущей v{currentVersion}. Резервная копия создана, данные сброшены.", LogLevel.Warning);
            Backup(path);
            return factory();
        }

        if (envelope.SchemaVersion < currentVersion)
        {
            var from = envelope.SchemaVersion;
            while (from < currentVersion)
            {
                data = migrate is not null ? migrate(data, from) : data;
                from++;
            }
            Save(path, data);
        }

        return data;
    }

    /// <summary>
    /// Подгрузить или создать новый файл
    /// </summary>
    public async Task<T> LoadOrCreateAsync<T>(string path, Func<T> factory, Func<T, int, T>? migrate = null)
    {
        if (!File.Exists(path))
        {
            return factory();
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path);
        }
        catch (IOException ex)
        {
            logger?.Log($"Не удалось прочитать '{path}': {ex.Message}", LogLevel.Warning);
            return factory();
        }

        SchemaEnvelope<T>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<SchemaEnvelope<T>>(json, jsonOptions);
        }
        catch (JsonException ex)
        {
            logger?.Log($"Файл '{path}' повреждён: {ex.Message}. Резервная копия создана, данные сброшены.", LogLevel.Warning);
            Backup(path);
            return factory();
        }

        // SchemaVersion == 0 => старый/чужой формат без конверта (делаем бэкап).
        if (envelope is null || envelope.SchemaVersion == 0)
        {
            logger?.Log($"Файл '{path}' имеет устаревший формат (нет версии схемы). Резервная копия создана, данные сброшены.", LogLevel.Warning);
            Backup(path);
            return factory();
        }

        var data = envelope.Data ?? factory();

        if (envelope.SchemaVersion > currentVersion)
        {
            logger?.Log($"Файл '{path}' (v{envelope.SchemaVersion}) новее текущей v{currentVersion}. Резервная копия создана, данные сброшены.", LogLevel.Warning);
            Backup(path);
            return factory();
        }

        if (envelope.SchemaVersion < currentVersion)
        {
            var from = envelope.SchemaVersion;
            while (from < currentVersion)
            {
                data = migrate is not null ? migrate(data, from) : data;
                from++;
            }
            await SaveAsync(path, data);
        }

        return data;
    }

    /// <summary>
    /// Сохранить данные
    /// </summary>
    public async Task SaveAsync<T>(string path, T data)
    {
        var envelope = new SchemaEnvelope<T> { SchemaVersion = currentVersion, Data = data };
        var json = JsonSerializer.Serialize(envelope, jsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>
    /// Сохранить данные
    /// </summary>
    public void Save<T>(string path, T data)
    {
        var envelope = new SchemaEnvelope<T> { SchemaVersion = currentVersion, Data = data };
        var json = JsonSerializer.Serialize(envelope, jsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    private static void Backup(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Copy(path, path + ".bak", overwrite: true);
            }
        }
        catch (IOException) { }
    }
}
