using System.Text.Json;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Models;

namespace MIN.Sessions.Core.Services.Services;

/// <summary>
/// Хранилище сохранённых туннельных сессий
/// </summary>
public class TunnelSessionStore : ITunnelSessionStore
{
    private const string ThumbnailFileName = "thumbnail.png";

    private readonly ISessionScanner sessionScanner;

    private readonly static JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TunnelSessionStore"/>
    /// </summary>
    public TunnelSessionStore(ISessionScanner sessionScanner)
    {
        this.sessionScanner = sessionScanner;
    }

    async Task ITunnelSessionStore.SaveAsync(Session session, byte[]? thumbnailData, CancellationToken cancellationToken)
    {
        session.ThumbnailFileName = thumbnailData != null && thumbnailData.Length > 0
             ? ThumbnailFileName
             : null;

        var sessionDirectory = Path.Combine(sessionScanner.SessionsDirectory, session.SessionId);
        Directory.CreateDirectory(sessionDirectory);

        var presenterPath = Path.Combine(sessionDirectory, "presenter.json");
        await using var stream = File.Create(presenterPath);
        await JsonSerializer.SerializeAsync(stream, session, jsonOptions, cancellationToken);

        var thumbnailPath = Path.Combine(sessionDirectory, ThumbnailFileName);

        if (thumbnailData != null && thumbnailData.Length > 0)
        {
            await File.WriteAllBytesAsync(thumbnailPath, thumbnailData, cancellationToken);
        }
        else if (File.Exists(thumbnailPath))
        {
            File.Delete(thumbnailPath);
        }
    }
}
