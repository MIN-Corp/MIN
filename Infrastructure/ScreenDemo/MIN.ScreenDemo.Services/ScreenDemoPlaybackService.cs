using System.Threading.Channels;
using MIN.Helpers.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Constants;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Services;

/// <inheritdoc cref="IScreenDemoPlaybackService"/>
public class ScreenDemoPlaybackService : IScreenDemoPlaybackService
{
    private sealed record DecodedJob(Guid RoomId, Guid SharerId, byte[] EncodedFrame);

    private readonly IScreenVideoCodec codec;
    private readonly ILoggerProvider logger;
    private readonly object gate = new();
    private readonly Dictionary<(Guid RoomId, Guid SharerId), DemoStreamState> streams = [];
    private readonly Channel<DecodedJob> decodeQueue;
    private readonly CancellationTokenSource decodeCts = new();
    private readonly Thread decodeThread;

    /// <inheritdoc />
    public event EventHandler<ScreenDemoFrameDecodedEventArgs>? FrameDecoded;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoPlaybackService"/>
    /// </summary>
    public ScreenDemoPlaybackService(IScreenVideoCodec codec, ILoggerProvider logger)
    {
        this.codec = codec;
        this.logger = logger;

        decodeQueue = Channel.CreateBounded<DecodedJob>(new BoundedChannelOptions(4)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest, // отстаём — декодируем более свежий кадр, старый роняем
        });

        decodeThread = new Thread(DecodeLoop)
        {
            Name = "ScreenDemoDecodeThread",
            IsBackground = true,
        };
        decodeThread.Start();
    }

    void IScreenDemoPlaybackService.AddStream(Guid roomId, Guid sharerId)
    {
        lock (gate)
        {
            streams.TryAdd((roomId, sharerId), new DemoStreamState());
        }
    }

    void IScreenDemoPlaybackService.RemoveStream(Guid roomId, Guid sharerId)
    {
        lock (gate)
        {
            streams.Remove((roomId, sharerId));
        }
    }

    void IScreenDemoPlaybackService.SubmitFragment(Guid roomId, Guid sharerId, long frameId,
        int fragmentIndex, int fragmentCount, bool isKeyframe, byte[] data)
    {
        byte[]? assembled;

        lock (gate)
        {
            if (!streams.TryGetValue((roomId, sharerId), out var state))
            {
                return;
            }

            // Кадр уже собран или устарел (пришли опоздавшие фрагменты)
            if (frameId <= state.LastCompletedFrameId)
            {
                return;
            }

            if (frameId != state.CurrentFrameId)
            {
                if (state.FragmentCount > 0 && state.FragmentsReceived < state.FragmentCount)
                {
                    var lost = state.FragmentCount - state.FragmentsReceived;
                    logger.Log($"Кадр {state.CurrentFrameId} от {sharerId} потерян (не хватило {lost} фрагментов), ждём ключевой кадр");
                }

                if (fragmentCount is < 1 or > ScreenDemoConstants.MaxFragmentsPerFrame)
                {
                    return;
                }

                state.CurrentFrameId = frameId;
                state.FragmentCount = fragmentCount;
                state.FragmentsReceived = 0;
                state.IsKeyframe = isKeyframe;
                state.Fragments = new byte[fragmentCount][];
                state.Received = new bool[fragmentCount];
            }

            if (fragmentIndex < 0 || fragmentIndex >= state.FragmentCount || state.Received[fragmentIndex])
            {
                return;
            }

            state.Fragments[fragmentIndex] = data;
            state.Received[fragmentIndex] = true;
            state.FragmentsReceived++;

            if (state.FragmentsReceived < state.FragmentCount)
            {
                return;
            }

            var totalLength = state.Fragments.Sum(f => f.Length);
            assembled = new byte[totalLength];
            var offset = 0;

            foreach (var fragment in state.Fragments)
            {
                Buffer.BlockCopy(fragment, 0, assembled, offset, fragment.Length);
                offset += fragment.Length;
            }

            state.LastCompletedFrameId = frameId;
            state.CurrentFrameId = -1;
            state.FragmentCount = 0;
        }

        // Декодирование вне блокировки — через очередь декодеру
        decodeQueue.Writer.TryWrite(new DecodedJob(roomId, sharerId, assembled));
    }

    void IScreenDemoPlaybackService.Clear()
    {
        lock (gate)
        {
            streams.Clear();
        }
    }

    void IDisposable.Dispose()
    {
        decodeCts.Cancel();
        decodeThread.Join();
        decodeCts.Dispose();
    }

    private void DecodeLoop()
    {
        try
        {
            foreach (var job in decodeQueue.Reader.ReadAllAsync(decodeCts.Token).ToBlockingEnumerable(decodeCts.Token))
            {
                try
                {
                    // До первого ключевого кадра декодер может отдавать пустые/битые кадры — это норма
                    var frame = codec.Decode(job.EncodedFrame, Environment.TickCount64);

                    FrameDecoded?.Invoke(this, new ScreenDemoFrameDecodedEventArgs
                    {
                        RoomId = job.RoomId,
                        SharerId = job.SharerId,
                        Frame = frame,
                    });
                }
                catch (Exception ex)
                {
                    logger.Log($"Ошибка декодирования кадра от {job.SharerId}: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
