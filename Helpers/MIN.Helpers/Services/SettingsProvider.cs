using MIN.Helpers.Contracts.Interfaces.SettingsServices;
using MIN.Helpers.Contracts.Models;

namespace MIN.Helpers.Services;

///<inheritdoc cref="ISettingsProvider"/>
public class SettingsProvider : ISettingsProvider
{
    private readonly ISettingsStorage storage;
    private Settings? cachedSettings;

    /// <inheritdoc />
    public Action? OnSettingsSaved { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SettingsProvider"/>
    /// </summary>
    public SettingsProvider(ISettingsStorage storage)
    {
        this.storage = storage;
    }

    Settings ISettingsProvider.GetSettings()
        => cachedSettings ??= storage.Load();

    async Task ISettingsProvider.SaveSettings(Settings settings)
    {
        cachedSettings = settings;
        await storage.Save(settings);
        OnSettingsSaved?.Invoke();
    }
}
