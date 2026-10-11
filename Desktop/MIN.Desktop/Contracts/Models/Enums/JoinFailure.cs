namespace MIN.Desktop.Contracts.Models.Enums;

/// <summary>
/// Тип ошибки при подключении
/// </summary>
public enum JoinFailure
{
    /// <summary>
    /// Отмена
    /// </summary>
    Cancelled,

    /// <summary>
    /// Комната была сменена
    /// </summary>
    Mismatch,

    /// <summary>
    /// Другая ошибка
    /// </summary>
    Error
}
