namespace MIN.Desktop.Contracts.Enums;

/// <summary>
/// Решение по паролю комнаты из окна создания/редактирования
/// </summary>
public enum RoomPasswordChange
{
    /// <summary>
    /// Оставить как было (или без пароля при создании)
    /// </summary>
    Keep,

    /// <summary>
    /// Установить новый пароль
    /// </summary>
    Set,

    /// <summary>
    /// Убрать пароль
    /// </summary>
    Clear,
}
