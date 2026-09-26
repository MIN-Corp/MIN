namespace MIN.Helpers.Contracts.Models;

/// <summary>
/// Обёртка персистентных данных с номером версии схемы.
/// Позволяет обнаруживать устаревший/несовместимый формат и мигрировать его.
/// </summary>
public sealed class SchemaEnvelope<T>
{
    /// <summary>
    /// Версия схемы
    /// </summary>
    public int SchemaVersion { get; set; }

    /// <summary>
    /// Данные в ней
    /// </summary>
    public T? Data { get; set; }
}
