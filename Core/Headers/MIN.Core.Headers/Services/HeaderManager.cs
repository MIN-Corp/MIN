using MIN.Core.Headers.Contracts.Constants;
using MIN.Core.Headers.Contracts.Enums;
using MIN.Core.Headers.Contracts.Interfaces;
using MIN.Core.Headers.Contracts.Models;

namespace MIN.Core.Headers.Services;

/// <summary>
/// <inheritdoc cref="IHeaderManager"/>
/// </summary>
public class HeaderManager : IHeaderManager
{
    byte[] IHeaderManager.AddHeader(byte[] data, byte header)
    {
        var result = new byte[1 + data.Length];
        result[0] = header;
        data.CopyTo(result, 1);
        return result;
    }

    Memory<byte> IHeaderManager.StripTypeHeader(byte[] data)
        => data.Length > 1 ? data.AsMemory(1) : Memory<byte>.Empty;

    bool IHeaderManager.IsEncrypted(ReadOnlySpan<byte> data)
        => !data.IsEmpty && (data[0] & (byte)HeaderMessageType.Encrypted) != 0;

    bool IHeaderManager.IsAck(ReadOnlySpan<byte> data)
        => !data.IsEmpty && (data[0] & (byte)HeaderMessageType.Ack) != 0;

    bool IHeaderManager.IsStreamChunk(ReadOnlySpan<byte> data)
        => !data.IsEmpty && (data[0] & (byte)HeaderMessageType.StreamChunk) != 0;

    /// <inheritdoc />
    public StreamChunkFlags GetStreamFlags(ReadOnlySpan<byte> data)
        => data.IsEmpty ? StreamChunkFlags.None : (StreamChunkFlags)(data[0] & ~(byte)HeaderMessageType.StreamChunk);

    byte[] IHeaderManager.BuildStreamChunkHeader(StreamChunkFlags flags, Guid streamId, int index, int total)
    {
        var header = new byte[HeadersConstants.StreamHeaderSize];
        ((IHeaderManager)this).WriteStreamChunkHeader(header, flags, streamId, index, total);
        return header;
    }

    void IHeaderManager.WriteStreamChunkHeader(Span<byte> destination, StreamChunkFlags flags, Guid streamId, int index, int total)
    {
        destination[0] = (byte)((byte)HeaderMessageType.StreamChunk | (byte)flags);
        streamId.TryWriteBytes(destination.Slice(1, 16));
        BitConverter.TryWriteBytes(destination.Slice(17, 4), index);
        BitConverter.TryWriteBytes(destination.Slice(21, 4), total);
    }

    StreamChunkHeader IHeaderManager.ParseStreamChunkHeader(ReadOnlySpan<byte> data)
        => new(
            ((IHeaderManager)this).GetStreamFlags(data),
            new Guid(data.Slice(1, 16)),
            BitConverter.ToInt32(data.Slice(17, 4)),
            BitConverter.ToInt32(data.Slice(21, 4))
        );
}
