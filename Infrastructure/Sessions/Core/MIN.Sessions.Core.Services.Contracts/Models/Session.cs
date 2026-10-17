using System.Text.Json.Serialization;
using MIN.Sessions.Core.Services.Contracts.Enums;

namespace MIN.Sessions.Core.Services.Contracts.Models;

/// <summary>
/// Сессия
/// </summary>
public class Session
{
    /// <summary>
    /// Идентификатор сессии
    /// </summary>
    public required string SessionId { get; set; }

    /// <summary>
    /// Название сессии
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Описание сессии
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Версия сессии (для совместимости)
    /// </summary>
    public required Version Version { get; set; }

    /// <summary>
    /// Максимальное допустимое количество участников в одной сессии
    /// </summary>
    /// <remarks>
    /// null если лимита нет
    /// </remarks>
    public int? MaximumParticipants { get; set; }

    /// <summary>
    /// Название программы сервера сессии
    /// </summary>
    public required string ServerExecutableFileName { get; set; }

    /// <summary>
    /// Название программы клиента сессии
    /// </summary>
    public required string ClientExecutableFileName { get; set; }

    /// <summary>
    /// Ссылка на скачивание сессии
    /// </summary>
    public required string DownloadLink { get; set; }

    /// <summary>
    /// Название файла обложки сессии
    /// </summary>
    /// <remarks>
    /// null если её нет
    /// </remarks>
    public string? ThumbnailFileName { get; set; }

    /// <summary>
    /// Путь к папке сессии
    /// </summary>
    [JsonIgnore]
    public string SessionDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Вид сессии
    /// </summary>
    public SessionKind Kind { get; set; } = SessionKind.Process;

    /// <summary>
    /// Порт, на котором слушает игровой сервер хоста (для сессий вида PortTunnel)
    /// </summary>
    public int? TunnelPort { get; set; }

    /// <summary>
    /// Получить путь к программе сервера сессии
    /// </summary>
    public string GetServerPath()
#if DEBUG
        // Для тестирования программ сюда вставлять путь туда, где она собирается
        => Path.Combine(SessionDirectory, ServerExecutableFileName);
#else
        => Path.Combine(SessionDirectory, ServerExecutableFileName);
#endif

    /// <summary>
    /// Получить путь к программе клиента сессии
    /// </summary>
    public string GetClientPath()
#if DEBUG
        => Path.Combine(SessionDirectory, ClientExecutableFileName);
#else
        => Path.Combine(SessionDirectory, ClientExecutableFileName);
#endif

    /// <summary>
    /// Получить путь к программе клиента сессии
    /// </summary>
    public string GetThumbnailPath()
        => Path.Combine(SessionDirectory, ThumbnailFileName ?? string.Empty);
}
