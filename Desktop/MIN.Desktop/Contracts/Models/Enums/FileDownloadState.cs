namespace MIN.Desktop.Contracts.Models.Enums;

/// <summary>
/// Состояния скачивания файла
/// </summary>
public enum FileDownloadState
{
    /// <summary>
    /// Скачен
    /// </summary>
    Downloaded,

    /// <summary>
    /// Скачиваеся
    /// </summary>
    IsDownloading,

    /// <summary>
    /// Не скачен
    /// </summary>
    NotDownloaded,

    /// <summary>
    /// Собирается
    /// </summary>
    Assembling,

    /// <summary>
    /// Без состояния
    /// </summary>
    /// <remarks>
    /// Нужен чтобы скрыть при наведении
    /// </remarks>
    None,
}
