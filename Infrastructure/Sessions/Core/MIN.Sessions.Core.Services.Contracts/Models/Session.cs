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
    public required string SessionId { get; init; }

    /// <summary>
    /// Название сессии
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Описание сессии
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Версия сессии (для совместимости)
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// Максимальное допустимое количество участников в одной сессии
    /// </summary>
    /// <remarks>
    /// Null если лимита нет
    /// </remarks>
    public int? MaximumParticipants { get; init; }

    /// <summary>
    /// Название программы сервера сессии
    /// </summary>
    public string ServerExecutableFileName { get; init; } = string.Empty;

    /// <summary>
    /// Название программы клиента сессии
    /// </summary>
    public string ClientExecutableFileName { get; init; } = string.Empty;

    /// <summary>
    /// Ссылка на скачивание сессии
    /// </summary>
    public string DownloadLink { get; init; } = string.Empty;

    /// <summary>
    /// Название файла обложки сессии
    /// </summary>
    /// <remarks>
    /// Null если её нет
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
    public SessionKind Kind { get; init; } = SessionKind.Process;

    /// <summary>
    /// Порт, на котором слушает игровой сервер хоста (для сессий вида PortTunnel)
    /// </summary>
    public int? TunnelPort { get; init; }

    /// <summary>
    /// Получить путь к программе сервера сессии
    /// </summary>
    public string GetServerPath()
#if DEBUG
        // Для тестирования программ сюда вставлять путь туда, где она собирается
        => @"C:\Users\Admin\Documents\CSharpProjects\Learning\Projects\MinChess\Network\MIN.Chess.Server\bin\Debug\net8.0-windows\win-x64\MIN.Chess.Server.exe";
#else
        => Path.Combine(SessionDirectory, ServerExecutableFileName);
#endif

    /// <summary>
    /// Получить путь к программе клиента сессии
    /// </summary>
    public string GetClientPath()
#if DEBUG
        => @"C:\Users\Admin\Documents\CSharpProjects\Learning\Projects\MinChess\Network\MIN.Chess.Client\bin\Debug\net8.0-windows\win-x64\MIN.Chess.Client.exe";
#else
        => Path.Combine(SessionDirectory, ClientExecutableFileName);
#endif

    /// <summary>
    /// Получить путь к программе клиента сессии
    /// </summary>
    public string GetThumbnailPath()
        => Path.Combine(SessionDirectory, ThumbnailFileName ?? string.Empty);
}
