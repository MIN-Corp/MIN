using System.Collections.Concurrent;
using FluentAssertions;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Headers.Contracts.Enums;
using MIN.Core.Headers.Services;
using MIN.Core.Streaming.Contracts.Constants;
using MIN.Core.Streaming.Contracts.Events;
using MIN.Core.Streaming.Contracts.Events.Receiving;
using MIN.Core.Streaming.Contracts.Events.Sending;
using MIN.Core.Streaming.Contracts.Interfaces;
using MIN.Core.Streaming.Contracts.Models;
using MIN.Tests.Common;
using Xunit;

namespace MIN.Core.Streaming.Tests;

/// <summary>
/// Сквозные тесты <see cref="StreamManager"/>: отправитель и получатель с реальным шифрованием
/// </summary>
public class StreamManagerTests
{
    /// <summary>
    /// Стенд: два узла, соединённых in-memory транспортом с маршрутизацией байтов
    /// </summary>
    private sealed class Harness : IDisposable
    {
        private EncryptorPair Pair { get; set; } = null!;
        private TestRoomFactory SenderRooms { get; } = new();
        public InMemoryTransport SenderTransport { get; } = new();
        public InMemoryTransport ReceiverTransport { get; } = new();
        public IStreamManager Sender { get; private set; } = null!;
        public Guid RoomId { get; } = Guid.NewGuid();
        private Guid SenderConnectionId { get; } = Guid.NewGuid();
        public Guid ReceiverConnectionId { get; } = Guid.NewGuid();
        public ConcurrentQueue<MessageAssembledEventArgs> Assembled { get; } = new();
        public ConcurrentDictionary<Guid, byte[]> RawFiles { get; } = new();
        public ConcurrentQueue<ChunkReceivedEventArgs> ChunksReceived { get; } = new();
        public ConcurrentQueue<ChunkSendedEventArgs> ChunksSent { get; } = new();
        public ConcurrentQueue<StreamCompletedEventArgs> Completed { get; } = new();
        public ConcurrentQueue<StreamFailedEventArgs> Failed { get; } = new();

        public static async Task<Harness> CreateAsync()
        {
            var harness = new Harness();
            harness.Pair = await EncryptorPair.CreateAsync();
            var headerManager = new HeaderManager();
            var logger = new TestLoggerProvider();

            var context = harness.SenderRooms.CreateContext(harness.RoomId);
            context.Connections.Register(harness.ReceiverConnectionId, new ParticipantInfo { Id = harness.Pair.BobId, Name = "Боб" });

            var senderManager = new StreamManager(harness.SenderTransport, harness.Pair.Alice, headerManager, harness.SenderRooms.Factory, logger);
            senderManager.ChunkSended += (e, _) => { harness.ChunksSent.Enqueue(e); return Task.CompletedTask; };
            senderManager.OnStreamCompleted += (e, _) => { harness.Completed.Enqueue(e); return Task.CompletedTask; };
            senderManager.OnStreamFailed += (e, _) => { harness.Failed.Enqueue(e); return Task.CompletedTask; };
            harness.Sender = senderManager;

            var receiverAssembler = new ChunkBufferAssembler(harness.ReceiverTransport, headerManager, logger);
            receiverAssembler.MessageAssembled += (e, _) =>
            {
                if (e.IsRawPayload && e.FilePath != null && File.Exists(e.FilePath))
                {
                    harness.RawFiles[e.StreamId] = File.ReadAllBytes(e.FilePath);
                }

                harness.Assembled.Enqueue(e);
                return Task.CompletedTask;
            };
            receiverAssembler.ChunkReceived += (e, _) => { harness.ChunksReceived.Enqueue(e); return Task.CompletedTask; };
            receiverAssembler.OnStreamFailed += (e, _) => { harness.Failed.Enqueue(e); return Task.CompletedTask; };

            harness.SenderTransport.SendHandler = async datagram =>
            {
                var data = datagram.Data;
                ReadOnlyMemory<byte> inner = data[0] == (byte)HeaderMessageType.Encrypted
                    ? harness.Pair.Bob.DecryptMessage(data.AsMemory(1), harness.Pair.AliceId)
                    : data.AsMemory(1);
                await ((IChunkBufferAssembler)receiverAssembler).ProcessStreamChunk(inner, harness.RoomId, harness.SenderConnectionId, null, CancellationToken.None);
            };
            harness.ReceiverTransport.SendHandler = datagram =>
            {
                harness.Sender.ProcessAck(datagram.Data);
                return Task.CompletedTask;
            };

            return harness;
        }

        public void Dispose()
        {
            ((IDisposable)Sender).Dispose();
            SenderRooms.Dispose();
            Pair.Dispose();
        }
    }

    /// <summary>
    /// Поток, возвращающий не более maxRead байт за одно чтение
    /// </summary>
    private sealed class PartialReadStream : Stream
    {
        private readonly byte[] data;
        private readonly int maxRead;
        private int position;

        public PartialReadStream(byte[] data, int maxRead)
        {
            this.data = data;
            this.maxRead = maxRead;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;
        public override long Position
        {
            get => position;
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = Math.Min(Math.Min(buffer.Length, maxRead), data.Length - position);
            data.AsSpan(position, count).CopyTo(buffer.Span);
            position += count;
            return ValueTask.FromResult(count);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = Math.Min(Math.Min(count, maxRead), data.Length - position);
            Array.Copy(data, position, buffer, offset, n);
            position += n;
            return n;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// Маленький payload без шифрования доходит одним чанком
    /// </summary>
    [Fact]
    public async Task SmallPlainPayloadShouldAssemble()
    {
        // Arrange
        using var harness = await Harness.CreateAsync();
        var payload = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();

        // Act
        await harness.Sender.SendAsync(payload, new StreamOptions(), harness.RoomId, harness.ReceiverConnectionId, null, CancellationToken.None);
        await TestWait.UntilAsync(() => !harness.Assembled.IsEmpty);

        // Assert
        harness.Assembled.Single().Data.Should().Equal(payload);
        harness.ChunksSent.Should().HaveCount(1);
        harness.Completed.Should().HaveCount(1);
    }

    /// <summary>
    /// Маленький payload с шифрованием доходит и расшифровывается
    /// </summary>
    [Fact]
    public async Task SmallEncryptedPayloadShouldAssemble()
    {
        // Arrange
        using var harness = await Harness.CreateAsync();
        var payload = "секретные данные"u8.ToArray();

        // Act
        await harness.Sender.SendAsync(payload, new StreamOptions { RequiresEncryption = true }, harness.RoomId, harness.ReceiverConnectionId, null, CancellationToken.None);
        await TestWait.UntilAsync(() => !harness.Assembled.IsEmpty);

        // Assert
        harness.Assembled.Single().Data.Should().Equal(payload);
        harness.SenderTransport.SentDatagrams.Single().Data[0].Should().Be((byte)HeaderMessageType.Encrypted);
    }

    /// <summary>
    /// Все пакеты многочанковой передачи доходят и собираются байт-в-байт
    /// </summary>
    [Fact]
    public async Task MultiChunkEncryptedShouldDeliverAllPackets()
    {
        // Arrange
        using var harness = await Harness.CreateAsync();
        var payload = new byte[StreamingConstants.ChunkDataSize * 3 + 123];
        Random.Shared.NextBytes(payload);
        const int expectedChunks = 4;

        // Act
        await harness.Sender.SendAsync(payload, new StreamOptions { RequiresEncryption = true }, harness.RoomId, harness.ReceiverConnectionId, null, CancellationToken.None);
        await TestWait.UntilAsync(() => !harness.Assembled.IsEmpty);

        // Assert
        harness.Assembled.Single().Data.Should().Equal(payload);
        harness.ChunksSent.Should().HaveCount(expectedChunks);
        harness.ChunksReceived.Should().HaveCount(expectedChunks);
        harness.ChunksSent.Last().ReceivedBytes.Should().Be(payload.Length);
    }

    /// <summary>
    /// Большой payload (1 МиБ) собирается целиком
    /// </summary>
    [Fact]
    public async Task LargePayloadShouldAssemble()
    {
        // Arrange
        using var harness = await Harness.CreateAsync();
        var payload = new byte[1024 * 1024];
        Random.Shared.NextBytes(payload);

        // Act
        await harness.Sender.SendAsync(payload, new StreamOptions { RequiresEncryption = true }, harness.RoomId, harness.ReceiverConnectionId, null, CancellationToken.None);
        await TestWait.UntilAsync(() => !harness.Assembled.IsEmpty, timeoutMs: 15000);

        // Assert
        harness.Assembled.Single().Data.Should().Equal(payload);
    }

    /// <summary>
    /// RequiresAcks: получатель подтверждает каждый чанк
    /// </summary>
    [Fact]
    public async Task RequiresAcksShouldAckEveryChunk()
    {
        // Arrange
        using var harness = await Harness.CreateAsync();
        var payload = new byte[StreamingConstants.ChunkDataSize * 2 + 10];

        // Act
        await harness.Sender.SendAsync(payload, new StreamOptions { RequiresEncryption = true, RequiresAcks = true }, harness.RoomId, harness.ReceiverConnectionId, null, CancellationToken.None);
        await TestWait.UntilAsync(() => harness.Assembled.Count >= 1);

        // Assert
        var acks = harness.ReceiverTransport.SentDatagrams.ToArray();
        acks.Should().HaveCount(3);
        acks.Should().OnlyContain(a => a.Data[0] == (byte)HeaderMessageType.Ack);
        acks.Select(a => BitConverter.ToInt32(a.Data, 17)).Should().Equal(0, 1, 2);
    }

    /// <summary>
    /// Пользовательский StreamId используется в передаче
    /// </summary>
    [Fact]
    public async Task CustomStreamIdShouldBeUsed()
    {
        // Arrange
        using var harness = await Harness.CreateAsync();
        var streamId = Guid.NewGuid();

        // Act
        await harness.Sender.SendAsync(new byte[] { 1 }, new StreamOptions { StreamId = streamId }, harness.RoomId, harness.ReceiverConnectionId, null, CancellationToken.None);
        await TestWait.UntilAsync(() => !harness.Assembled.IsEmpty);

        // Assert
        harness.Assembled.Single().StreamId.Should().Be(streamId);
        harness.Completed.Single().StreamId.Should().Be(streamId);
    }

    /// <summary>
    /// Пустой byte[] payload должен доставляться как пустое сообщение (как и Stream-перегрузка)
    /// </summary>
    [Fact]
    public async Task EmptyByteArrayShouldDeliverEmptyMessage()
    {
        // Arrange
        using var harness = await Harness.CreateAsync();

        // Act
        await harness.Sender.SendAsync(ReadOnlyMemory<byte>.Empty, new StreamOptions(), harness.RoomId, harness.ReceiverConnectionId, null, CancellationToken.None);
        await TestWait.UntilAsync(() => !harness.Assembled.IsEmpty, timeoutMs: 2000);

        // Assert
        harness.Assembled.Single().Data.Should().BeEmpty();
    }

    /// <summary>
    /// Пустой Stream доставляется как пустое сообщение
    /// </summary>
    [Fact]
    public async Task EmptyStreamShouldDeliverEmptyMessage()
    {
        // Arrange
        using var harness = await Harness.CreateAsync();
        using var source = new MemoryStream();

        // Act
        await harness.Sender.SendAsync(source, new StreamOptions { IsRawPayload = true }, harness.RoomId, harness.ReceiverConnectionId, null, CancellationToken.None);
        await TestWait.UntilAsync(() => !harness.Assembled.IsEmpty);

        // Assert
        harness.Assembled.Single().Data.Should().BeEmpty();
    }

    /// <summary>
    /// Stream с частичными чтениями собирается корректно (чанки не должны ломать нумерацию)
    /// </summary>
    [Fact]
    public async Task StreamWithPartialReadsShouldAssemble()
    {
        // Arrange
        using var harness = await Harness.CreateAsync();
        var payload = new byte[40_000];
        Random.Shared.NextBytes(payload);
        await using var source = new PartialReadStream(payload, maxRead: 1000);

        // Act
        await harness.Sender.SendAsync(source, new StreamOptions { RequiresEncryption = true, IsRawPayload = true }, harness.RoomId, harness.ReceiverConnectionId, null, CancellationToken.None);
        await TestWait.UntilAsync(() => !harness.Assembled.IsEmpty, timeoutMs: 10000);

        // Assert
        harness.RawFiles.Should().ContainKey(harness.Assembled.Single().StreamId);
        harness.RawFiles[harness.Assembled.Single().StreamId].Should().Equal(payload);
    }

    /// <summary>
    /// Raw payload из Stream доставляется файлом с верным содержимым
    /// </summary>
    [Fact]
    public async Task RawStreamShouldDeliverFile()
    {
        // Arrange
        using var harness = await Harness.CreateAsync();
        var payload = new byte[StreamingConstants.ChunkDataSize * 2 + 777];
        Random.Shared.NextBytes(payload);
        using var source = new MemoryStream(payload);

        // Act
        await harness.Sender.SendAsync(source, new StreamOptions { RequiresEncryption = true, IsRawPayload = true }, harness.RoomId, harness.ReceiverConnectionId, null, CancellationToken.None);
        await TestWait.UntilAsync(() => !harness.Assembled.IsEmpty, timeoutMs: 10000);

        // Assert
        var assembled = harness.Assembled.Single();
        assembled.IsRawPayload.Should().BeTrue();
        assembled.FilePath.Should().NotBeNull();
        harness.RawFiles[assembled.StreamId].Should().Equal(payload);
    }

    /// <summary>
    /// Отмена передачи проваливает поток с событием
    /// </summary>
    [Fact]
    public async Task CancellationShouldFailStream()
    {
        // Arrange
        using var harness = await Harness.CreateAsync();
        var payload = new byte[StreamingConstants.ChunkDataSize * 100];
        using var cts = new CancellationTokenSource();
        var slowTransport = harness.SenderTransport;
        var originalHandler = slowTransport.SendHandler;
        slowTransport.SendHandler = async datagram =>
        {
            await Task.Delay(20, cts.Token);
            if (originalHandler != null)
            {
                await originalHandler(datagram);
            }
        };
        cts.CancelAfter(150);

        // Act
        await harness.Sender.SendAsync(payload, new StreamOptions(), harness.RoomId, harness.ReceiverConnectionId, null, cts.Token);
        await TestWait.UntilAsync(() => !harness.Failed.IsEmpty);

        // Assert
        harness.Failed.Should().NotBeEmpty();
        harness.Completed.Should().BeEmpty();
    }
}
