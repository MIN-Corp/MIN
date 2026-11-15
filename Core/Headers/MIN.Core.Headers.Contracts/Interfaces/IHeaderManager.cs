using MIN.Core.Headers.Contracts.Enums;
using MIN.Core.Headers.Contracts.Models;

namespace MIN.Core.Headers.Contracts.Interfaces;

/// <summary>
/// Сервис по работе с заголовками пакетов данных
/// </summary>
/// <remarks>
/// <para><b>Незашифрованное сообщение (Plain):</b> [0x00][сериализованные_данные]</para>
/// <para><b>Зашифрованное сообщение:</b> [0x01][IV(12)][зашифрованные_данные][authTag(16)]</para>
/// <para><b>Пакет потока:</b> [0x10 + flags(0x0F)][streamId(16)][index(4)][total(4)][сериализованные_данные]</para>
/// <para><b>ACK:</b> [0x80][streamId(16)][index(4)]</para>
/// </remarks>
public interface IHeaderManager
{
    /// <summary>
    /// Добавить заголовок
    /// </summary>
    byte[] AddHeader(byte[] data, byte header);

    /// <summary>
    /// Проверяет по заголовку, зашифровано ли сообщение
    /// </summary>
    bool IsEncrypted(ReadOnlySpan<byte> data);

    /// <summary>
    /// Проверяет по заголовку, является ли сообщение подтверждением пакета потока
    /// </summary>
    bool IsAck(ReadOnlySpan<byte> data);

    /// <summary>
    /// Убрать тип-заголовок (первый байт) без копирования — возвращает срез исходного массива
    /// </summary>
    /// <remarks>Срез валиден, пока валиден исходный буфер <paramref name="data"/></remarks>
    Memory<byte> StripTypeHeader(byte[] data);

    /// <summary>
    /// Проверяет по заголовку, является ли сообщение пакетом потока
    /// </summary>
    bool IsStreamChunk(ReadOnlySpan<byte> data);

    /// <summary>
    /// Получить заголовки пакета потока
    /// </summary>
    StreamChunkFlags GetStreamFlags(ReadOnlySpan<byte> data);

    /// <summary>
    /// Создать заголовок для пакета потока
    /// </summary>
    byte[] BuildStreamChunkHeader(StreamChunkFlags flags, Guid streamId, int index, int total);

    /// <summary>
    /// Записать заголовок пакета потока в подготовленный буфер
    /// </summary>
    void WriteStreamChunkHeader(Span<byte> destination, StreamChunkFlags flags, Guid streamId, int index, int total);

    /// <summary>
    /// Распарсить заголовок пакета потока
    /// </summary>
    StreamChunkHeader ParseStreamChunkHeader(ReadOnlySpan<byte> data);
}
