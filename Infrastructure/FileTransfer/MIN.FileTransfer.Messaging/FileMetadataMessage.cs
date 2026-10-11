using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Messaging.Contracts.Messages;

namespace MIN.FileTransfer.Messaging;

/// <summary>
/// Сообщения мета-данные файла
/// </summary>
public class FileMetadataMessage : BaseContentMessage, IDescribable, IReplyable, IMessageWithSecuredFields
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.FileMetadata;

    /// <inheritdoc />
    public override bool RequireStreamAcks => true;

    /// <inheritdoc />
    public override bool RequiresLocalDuplication => true;

    /// <summary>
    /// Отправитель сообщения
    /// </summary>
    public ParticipantInfo Sender { get; set; } = null!;

    /// <summary>
    /// Идентификатор потока, по которому придёт файл
    /// </summary>
    public Guid TransferId { get; set; }

    /// <summary>
    /// Название файла
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Размер файла
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Путь к файлу
    /// </summary>
    /// <remarks>
    /// null, если получен с хоста и не имеется локально
    /// </remarks>
    public string? FilePath { get; set; }

    /// <summary>
    /// Можно ли отправлять дальше
    /// </summary>
    /// <remarks>
    /// true - если он загружен на сервер и может быть передан дальше
    /// false - если ещё ожидается его загрузка и даже сам хост не получает информацию о нём
    /// </remarks>
    public bool AsDownloaded { get; set; }

    /// <inheritdoc />
    public Guid? ReplyToMessageId { get; set; }

    /// <inheritdoc />
    public string? ReplyToMessageDescription { get; set; }

    string IDescribable.GetDescription() => $"{Sender.Name}: {FileName}";

    IMessage IMessageWithSecuredFields.Sanitize()
        => new FileMetadataMessage(this) { FilePath = null };

    /// <summary>
    /// Инициализирует новый экзмемпляр <see cref="FileMetadataMessage"/>
    /// </summary>
    public FileMetadataMessage() { }

    /// <summary>
    /// Делает копию
    /// </summary>
    public FileMetadataMessage(FileMetadataMessage metadata)
    {
        Id = metadata.Id;
        Content = metadata.Content;
        IsUpdated = metadata.IsUpdated;
        UpdatedAt = metadata.UpdatedAt;
        SenderId = metadata.SenderId;
        Sender = metadata.Sender;
        FileSize = metadata.FileSize;
        FileName = metadata.FileName;
        FilePath = metadata.FilePath;
        RecipientId = metadata.RecipientId;
        ReplyToMessageId = metadata.ReplyToMessageId;
        ReplyToMessageDescription = metadata.ReplyToMessageDescription;
        TransferId = metadata.TransferId;
        Timestamp = metadata.Timestamp;
        AsDownloaded = metadata.AsDownloaded;
    }
}
