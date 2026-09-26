using MIN.Helpers.Contracts.Models;

namespace MIN.Helpers.Contracts.Interfaces.SettingsServices;

/// <summary>
/// Сервис по работе с настройками
/// </summary>
public interface ISettingsProvider
{
    /// <summary>
    /// Получить настройки
    /// </summary>
    Settings GetSettings();

    /// <summary>
    /// Сохранить настройки
    /// </summary>
    Task SaveSettings(Settings settings);

    /// <summary>
    /// Событие по сохранению настроек
    /// </summary>
    Action? OnSettingsSaved { get; set; }
}
