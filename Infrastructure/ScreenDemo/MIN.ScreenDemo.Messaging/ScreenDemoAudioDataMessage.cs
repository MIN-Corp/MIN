using MIN.Core.Messaging.Contracts;
using MIN.Core.Messaging.Contracts.Enums;
using MIN.Core.Messaging.Contracts.Messages;
using MIN.Core.SubRooms.Contracts.Interfaces.Messages;

namespace MIN.ScreenDemo.Messaging;

/// <summary>
/// Кадр звука демонстрации экрана.
/// Формат фиксирован контрактом: s16le, стерео, 48 кГц, 20 мс (3840 байта) —
/// вмещается в один UDP-пакет, фрагментация не нужна
/// </summary>
public sealed class ScreenDemoAudioDataMessage : BaseMessage, IWithinSubRoom
{
    /// <inheritdoc />
    public override MessageTypeTag TypeTag => MessageTypeTag.ScreenDemoAudioData;

    /// <inheritdoc />
    public override MessageChannel Channel => MessageChannel.Fast;

    /// <inheritdoc />
    public override bool RequireStreamAcks => false;

    /// <summary>
    /// Локальная дупликация не нужна: демонстрирующий не слушает
    /// свой собственный звук демонстрации
    /// </summary>
    public override bool RequiresLocalDuplication => false;

    /// <inheritdoc />
    public int SubRoomId { get; set; }

    /// <summary>
    /// Порядковый номер кадра звука в рамках демонстрации.
    /// Монотонный на всё время демонстрации: не сбрасывается между волнами
    /// зрителей, чтобы буферы переупорядочивания на зрителе не теряли поток
    /// </summary>
    public long SequenceNumber { get; init; }

    /// <summary>
    /// Таймштамп захвата (Environment.TickCount64 демонстрирующего) —
    /// та же шкала, что у кадров видео, для будущей синхронизации A/V
    /// </summary>
    public long TimestampMs { get; init; }

    /// <summary>
    /// Содержимое кадра (PCM s16le stereo 48 kHz)
    /// </summary>
    public required byte[] Data { get; init; }
}
