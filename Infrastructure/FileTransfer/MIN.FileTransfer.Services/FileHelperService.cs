using System.Collections.Concurrent;
using MIN.FileTransfer.Services.Contracts.Constants;
using MIN.FileTransfer.Services.Contracts.Interfaces;

namespace MIN.FileTransfer.Services;

/// <inheritdoc cref="IFileHelperService"/>
public sealed class FileHelperService : IFileHelperService
{
    private readonly static char[] invalidFileNameChars =
        Path.GetInvalidFileNameChars()
            .Concat(['\\', '/', ':', '*', '?', '"', '<', '>', '|'])
            .Distinct()
            .ToArray();

    private const string EmptyFileName = "безымянный_файл";

    private readonly static ConcurrentDictionary<string, string> mimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = "text/plain",
        [".html"] = "text/html",
        [".css"] = "text/css",
        [".js"] = "application/javascript",
        [".json"] = "application/json",
        [".xml"] = "application/xml",
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".zip"] = "application/zip",
        [".rar"] = "application/x-rar-compressed",
        [".7z"] = "application/x-7z-compressed",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".webp"] = "image/webp",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".ogg"] = "audio/ogg",
        [".mp4"] = "video/mp4",
        [".avi"] = "video/x-msvideo",
        [".mkv"] = "video/x-matroska",
        [".webm"] = "video/webm",
        [".exe"] = "application/x-msdownload",
        [".dll"] = "application/x-msdownload",
        [".msi"] = "application/x-msdownload",
    };

    private readonly static HashSet<string> blockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bat", ".cmd", ".com", ".scr", ".vbs", ".ps1", ".wsf", ".reg", ".inf",
        ".sh", ".bash", ".zsh", ".fish",  // Linux
        ".command", ".sh", // macOS
    };

    private readonly static HashSet<string> imageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".ico", ".jfif"};

    bool IFileHelperService.IsFileImage(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(extension) && imageExtensions.Contains(extension);
    }

    /// <inheritdoc />
    public string GetMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return mimeTypes.GetValueOrDefault(extension, "application/octet-stream");
    }

    string IFileHelperService.GetFileType(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension == string.Empty ? GetMimeType(fileName) : extension.Substring(1);
    }

    long IFileHelperService.GetFileSize(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length;
    }

    bool IFileHelperService.IsFileSizeAllowed(long fileSize)
        => fileSize > 0 && fileSize <= FileTransferConstants.MaximumFileSize;

    bool IFileHelperService.IsExtensionAllowed(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(extension) && !blockedExtensions.Contains(extension);
    }

    string IFileHelperService.SanitizeFileName(string fileName)
    {
        var sanitized = new string(fileName
            .Where(c => !invalidFileNameChars.Contains(c))
            .ToArray())
            .Trim();

        return string.IsNullOrEmpty(sanitized) ? EmptyFileName : sanitized;
    }

    string IFileHelperService.FormatFileSize(long bytes)
        => bytes switch
        {
            < 0 => throw new ArgumentException("Размер файла не может быть отрицательным", nameof(bytes)),
            < 1024 => $"{bytes} байт",
            < 1024 * 1024 => $"{(bytes / 1024.0):F2} КБ".Replace('.', ','),
            < 1024L * 1024 * 1024 => $"{(bytes / (1024.0 * 1024)):F2} МБ".Replace('.', ','),
            _ => $"{(bytes / (1024.0 * 1024 * 1024)):F2} ГБ".Replace('.', ',')
        };
}
