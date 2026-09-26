using MIN.Core.Services.Contracts.Exceptions;

namespace MIN.Desktop.Contracts.Enums;

/// <summary>
/// Выбор дальнейших дейстий при <see cref="RoomIdentityMismatchException"/>
/// </summary>
public enum RoomMismatchChoice
{
    /// <summary>
    /// Никакой (по умолчанию <see cref="Cancel"/>)
    /// </summary>
    None,

    /// <summary>
    /// Войти как в новую
    /// </summary>
    JoinNew,

    /// <summary>
    /// Заменить старой
    /// </summary>
    Replace,

    /// <summary>
    /// Отменить дальнейшее подключение
    /// </summary>
    Cancel
}
