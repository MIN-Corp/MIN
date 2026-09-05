using MIN.Helpers.Contracts.Models;

namespace MIN.Helpers.Contracts.Interfaces.SettingsServices;

/// <summary>
/// Сервис для хранения настроек
/// </summary>
public interface ISettingsStorage
{
    /// <summary>
    /// Загрузить настройки
    /// </summary>
    Settings Load();

    /// <summary>
    /// Сохранить настройки
    /// </summary>
    Task Save(Settings settings);
}
