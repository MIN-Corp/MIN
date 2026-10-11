using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MIN.Core.Cryptography.Contracts.Interfaces;
using MIN.Core.Entities;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Serialization.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Interfaces.Persistence;
using MIN.Core.Stores.Contracts.Models.Persistence;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Core.Stores.Services.Persistence;

/// <inheritdoc cref="IRoomFileStore"/>
public sealed class RoomFileStore : IRoomFileStore
{
    private const string FileExtension = ".mr";
    private const string BackupExtension = ".bak";
    private const string Magic = "MINR";
    private const byte FormatVersion = 1;
    private const int CurrentSchemaVersion = 1;
    private const int MaxNameLength = 40;
    private static int HeaderSize => Magic.Length + sizeof(byte);

    private readonly IRoomFileEncryptor encryptor;
    private readonly IMessageSerializer serializer;
    private readonly ILoggerProvider logger;
    private readonly IRoomStore roomStore;
    private readonly JsonSerializerOptions serializerOptions;
    private readonly string roomsDirectory;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomFileStore"/>
    /// </summary>
    public RoomFileStore(IRoomFileEncryptor encryptor,
        IMessageSerializer serializer,
        IIdentityDataPathProvider identityDataPath,
        IRoomStore roomStore,
        ILoggerProvider logger)
    {
        this.encryptor = encryptor;
        this.serializer = serializer;
        this.roomStore = roomStore;
        this.logger = logger;
        serializerOptions = serializer.SerializerOptions;
        roomsDirectory = identityDataPath.RoomsDirectory;
    }

    async Task IRoomFileStore.SaveAsync(Guid roomId, RoomSnapshot snapshot)
    {
        var envelope = new RoomFileEnvelope
        {
            SavedAt = DateTime.UtcNow,
            SchemaVersion = CurrentSchemaVersion,
            Room = CloneRoomWithoutChatHistory(snapshot.Room),
            Messages = snapshot.Messages.Select(SerializeMessage).ToList(),
            SubRooms = snapshot.SubRooms,
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, serializerOptions);
        var encrypted = encryptor.Protect(Compress(json));

        var payload = new byte[HeaderSize + encrypted.Length];
        Encoding.ASCII.GetBytes(Magic).CopyTo(payload, 0);
        payload[Magic.Length] = FormatVersion;
        encrypted.CopyTo(payload, HeaderSize);

        var path = GetFilePath(roomId);
        await WriteAtomicAsync(path, payload);
        DeleteDuplicates(roomId, keepPath: path);
    }

    IReadOnlyList<RoomSnapshot> IRoomFileStore.LoadAll()
    {
        Directory.CreateDirectory(roomsDirectory);

        var byId = new Dictionary<Guid, (string Path, RoomSnapshot Snapshot, DateTime SavedAt)>();

        foreach (var path in Directory.EnumerateFiles(roomsDirectory, $"*{FileExtension}"))
        {
            var loaded = TryLoad(path);
            if (loaded == null)
            {
                continue;
            }

            var id = loaded.Value.Snapshot.Room.Id;

            if (!byId.TryGetValue(id, out var existing))
            {
                byId[id] = loaded.Value;
                continue;
            }

            if (loaded.Value.SavedAt > existing.SavedAt)
            {
                DeleteIfExists(existing.Path);
                byId[id] = loaded.Value;
                logger.Log($"Найдены дубли файла комнаты {id}: оставлен '{loaded.Value.Path}', удалён '{existing.Path}'", LogLevel.Warning);
            }
            else
            {
                DeleteIfExists(loaded.Value.Path);
                logger.Log($"Найдены дубли файла комнаты {id}: оставлен '{existing.Path}', удалён '{loaded.Value.Path}'", LogLevel.Warning);
            }
        }

        return byId.Values.Select(v => v.Snapshot).ToList();
    }

    void IRoomFileStore.Delete(Guid roomId)
    {
        var candidates = Directory.EnumerateFiles(roomsDirectory, $"*{roomId}*")
            .Where(p => p.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)
                     || p.EndsWith(FileExtension + BackupExtension, StringComparison.OrdinalIgnoreCase)
                     || p.EndsWith(FileExtension + ".tmp", StringComparison.OrdinalIgnoreCase));

        foreach (var path in candidates)
        {
            DeleteIfExists(path);
        }
    }

    bool IRoomFileStore.Exists(Guid roomId)
        => File.Exists(GetFilePath(roomId));

    private (string Path, RoomSnapshot Snapshot, DateTime SavedAt)? TryLoad(string path)
    {
        byte[] payload;
        RoomFileEnvelope envelope;

        try
        {
            payload = File.ReadAllBytes(path);
            ValidateHeader(payload);

            var json = Decompress(encryptor.DecryptMessage(payload[HeaderSize..]));
            envelope = JsonSerializer.Deserialize<RoomFileEnvelope>(json, serializerOptions)
                ?? throw new InvalidDataException("Пустой конверт файла комнаты");
        }
        catch (Exception ex)
        {
            logger.Log($"Файл комнаты '{path}' повреждён: {ex.Message}. Резервная копия создана, комната пропущена.",
                LogLevel.Error);
            Backup(path);
            return null;
        }

        var messages = new List<IMessage>(envelope.Messages.Count);

        foreach (var messageJson in envelope.Messages)
        {
            try
            {
                messages.Add(serializer.Deserialize(Encoding.UTF8.GetBytes(messageJson)));
            }
            catch (Exception ex)
            {
                logger.Log($"Сообщение в '{path}' пропущено (устаревший формат?): {ex.Message}", LogLevel.Warning);
            }
        }

        if (envelope.Room == null)
        {
            logger.Log($"Файл комнаты '{path}' не содержит комнату. Резервная копия создана (одноразово), комната пропущена.",
                LogLevel.Error);
            Backup(path);
            return null;
        }

        return new(path, new RoomSnapshot
        {
            Room = envelope.Room,
            Messages = messages,
            SubRooms = envelope.SubRooms,
        }, envelope.SavedAt);
    }

    private void DeleteDuplicates(Guid roomId, string keepPath)
    {
        var candidates = Directory.EnumerateFiles(roomsDirectory, $"*_{roomId}{FileExtension}")
            .Concat(Directory.EnumerateFiles(roomsDirectory, $"{roomId}{FileExtension}"));

        foreach (var path in candidates)
        {
            if (path == keepPath)
            {
                continue;
            }

            DeleteIfExists(path);
            DeleteIfExists(path + BackupExtension);
            logger.Log($"Удалён дублей-файл комнаты {roomId}: '{path}'", LogLevel.Information);
        }
    }

    private static void ValidateHeader(byte[] payload)
    {
        if (payload.Length <= HeaderSize)
        {
            throw new InvalidDataException("Файл слишком мал");
        }

        if (!Encoding.ASCII.GetBytes(Magic).AsSpan().SequenceEqual(payload.AsSpan(0, Magic.Length)))
        {
            throw new InvalidDataException("Неверная сигнатура файла");
        }

        var version = payload[Magic.Length];

        if (version != FormatVersion)
        {
            throw new InvalidDataException($"Неподдерживаемая версия формата файла: {version}");
        }
    }

    private string SerializeMessage(IMessage message)
        => Encoding.UTF8.GetString(serializer.Serialize(message));

    private static byte[] Compress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();

        using (var brotli = new BrotliStream(output, CompressionLevel.Optimal))
        {
            input.CopyTo(brotli);
        }

        return output.ToArray();
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        brotli.CopyTo(output);
        return output.ToArray();
    }

    private async Task WriteAtomicAsync(string path, byte[] payload)
    {
        Directory.CreateDirectory(roomsDirectory);

        var tempPath = path + ".tmp";
        await File.WriteAllBytesAsync(tempPath, payload);
        File.Move(tempPath, path, overwrite: true);
    }

    private void Backup(string path)
    {
        var backupPath = path + BackupExtension;

        try
        {
            if (File.Exists(path) && !File.Exists(backupPath))
            {
                File.Copy(path, backupPath);
            }
        }
        catch (IOException ex)
        {
            logger.Log($"Не удалось создать резервную копию '{backupPath}': {ex.Message}", LogLevel.Warning);
        }
    }

    private void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException ex)
        {
            logger.Log($"Не удалось удалить '{path}': {ex.Message}", LogLevel.Warning);
        }
    }

    private static Room CloneRoomWithoutChatHistory(Room room)
    {
        var clone = room.Clone();
        clone.ChatHistory = [];
        return clone;
    }

    private string GetFilePath(Guid roomId)
    {
        var name = GetRoomName(roomId);
        name = name != null ? $"{name}_" : string.Empty;
        return Path.Combine(roomsDirectory, $"{SanitizeRoomName(name)}{roomId}{FileExtension}");
    }

    private string? GetRoomName(Guid roomId)
        => roomStore.TryGetRoom(roomId, out var room) ? room.Name : null;

    private static string SanitizeRoomName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            builder.Append(invalidChars.Contains(c) ? '_' : c);
        }
        var result = builder.ToString().Trim().TrimEnd('.', ' ');
        if (result.Length > MaxNameLength)
        {
            result = result[..MaxNameLength].TrimEnd('.', ' ');
        }
        return result.Length == 0 ? "room" : result;
    }
}
