using System.Threading.Channels;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.Stores.Contracts.Models.SubRooms;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;
using MIN.ScreenDemo.Messaging;
using MIN.ScreenDemo.Services.Contracts.Constants;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;
using MIN.ScreenDemo.Services.Helpers;

namespace MIN.ScreenDemo.Services;

/// <inheritdoc cref="IScreenDemoTransmitter"/>
public class ScreenDemoTransmitter : IScreenDemoTransmitter
{
    private readonly IScreenCaptureService screenCaptureService;
    private readonly IScreenVideoCodec codec;
    private readonly IMessageRouter messageRouter;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    private Channel<ScreenFrame> captureQueue = null!;
    private Channel<ScreenDemoFrameDataMessage> sendQueue = null!;
    private CancellationTokenSource? sendCts;

    private SubRoomContext context;
    private long frameId;
    private int framesSinceLastKeyframe;
    private long lastFrameTimestamp;
    private int lastWidth;
    private int lastHeight;
    private volatile bool forceKeyframe;
    private bool isActive;

    private readonly object viewersGate = new();
    private readonly HashSet<Guid> viewers = [];

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoTransmitter"/>
    /// </summary>
    public ScreenDemoTransmitter(IScreenCaptureService screenCaptureService,
        IScreenVideoCodec codec,
        IMessageRouter messageRouter,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.screenCaptureService = screenCaptureService;
        this.codec = codec;
        this.messageRouter = messageRouter;
        this.identityService = identityService;
        this.logger = logger;
    }

    void IScreenDemoTransmitter.Begin(SubRoomContext subRoomContext, ScreenSourceInfo source)
    {
        if (isActive)
        {
            return;
        }

        context = subRoomContext;
        isActive = true;
        frameId = 0;
        framesSinceLastKeyframe = 0;
        lastFrameTimestamp = 0;
        lastWidth = 0;
        lastHeight = 0;
        forceKeyframe = true;

        lock (viewersGate)
        {
            viewers.Clear();
        }

        captureQueue = Channel.CreateBounded<ScreenFrame>(new BoundedChannelOptions(4)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest, // кодирование не успевает — роняем старые кадры
        });

        sendQueue = Channel.CreateBounded<ScreenDemoFrameDataMessage>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        sendCts = new CancellationTokenSource();
        _ = ProcessCapturedFramesAsync(sendCts.Token);
        _ = SendPumpAsync(sendCts.Token);

        screenCaptureService.FrameCaptured += OnFrameCaptured;
        screenCaptureService.Start(source);
    }

    void IScreenDemoTransmitter.AddViewer(Guid participantId)
    {
        lock (viewersGate)
        {
            if (viewers.Add(participantId))
            {
                // новому зрителю нужен ключевой кадр, чтобы декодер стартовал
                forceKeyframe = true;
            }
        }
    }

    void IScreenDemoTransmitter.RemoveViewer(Guid participantId)
    {
        lock (viewersGate)
        {
            viewers.Remove(participantId);
        }
    }


    /// <inheritdoc />
    public void End()
    {
        if (!isActive)
        {
            return;
        }

        isActive = false;

        screenCaptureService.FrameCaptured -= OnFrameCaptured;
        screenCaptureService.Stop();

        sendCts?.Cancel();
        sendCts = null;
        captureQueue.Writer.TryComplete();
        sendQueue.Writer.TryComplete();

        lock (viewersGate)
        {
            viewers.Clear();
        }
    }

    void IDisposable.Dispose() => End();

    private bool HasViewers
    {
        get
        {
            lock (viewersGate)
            {
                return viewers.Count > 0;
            }
        }
    }

    private void OnFrameCaptured(object? sender, ScreenFrame e)
    {
        if (!isActive)
        {
            return;
        }

        captureQueue.Writer.TryWrite(e);
    }

    private async Task ProcessCapturedFramesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in captureQueue.Reader.ReadAllAsync(cancellationToken))
            {
                // Троттлинг до ScreenDemoConstants.Fps
                if (frame.TimestampMs - lastFrameTimestamp < ScreenDemoConstants.FrameIntervalMs)
                {
                    continue;
                }

                // Зрителей нет — демонстрация только локальная, кодирование не тратим
                if (!HasViewers)
                {
                    continue;
                }

                var scaled = ScreenFrameScaler.Downscale(frame, ScreenDemoConstants.MaxFrameHeight);

                // Размер кадра изменился (изменение размера окна) — переконфигурация кодека
                if (scaled.Width != lastWidth || scaled.Height != lastHeight)
                {
                    lastWidth = scaled.Width;
                    lastHeight = scaled.Height;
                    forceKeyframe = true;
                }

                var needKeyframe = forceKeyframe || framesSinceLastKeyframe >= ScreenDemoConstants.KeyframeIntervalFrames;

                if (needKeyframe)
                {
                    // Переконфигурация сбрасывает кодек — следующий кадр всегда ключевой
                    codec.Configure(scaled.Width, scaled.Height, ScreenDemoConstants.TargetBitrateKbps);
                    framesSinceLastKeyframe = 0;
                    forceKeyframe = false;
                }

                var currentFrameId = Interlocked.Increment(ref frameId);
                var encoded = codec.Encode(scaled, out var isKeyframe);

                framesSinceLastKeyframe = isKeyframe ? 0 : framesSinceLastKeyframe + 1;
                lastFrameTimestamp = frame.TimestampMs;

                if (encoded.Length == 0)
                {
                    continue;
                }

                foreach (var fragment in FragmentFrame(currentFrameId, isKeyframe, encoded))
                {
                    sendQueue.Writer.TryWrite(fragment);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private IEnumerable<ScreenDemoFrameDataMessage> FragmentFrame(long currentFrameId, bool isKeyframe, byte[] encoded)
    {
        var fragmentCount = (int)Math.Ceiling(encoded.Length / (double)ScreenDemoConstants.FragmentTargetSize);

        if (fragmentCount > ScreenDemoConstants.MaxFragmentsPerFrame)
        {
            logger.Log($"Кадр {currentFrameId} не влез в {ScreenDemoConstants.MaxFragmentsPerFrame} фрагментов ({encoded.Length} байт) — отброшен", LogLevel.Warning);
            yield break;
        }

        for (var i = 0; i < fragmentCount; i++)
        {
            var offset = i * ScreenDemoConstants.FragmentTargetSize;
            var length = Math.Min(ScreenDemoConstants.FragmentTargetSize, encoded.Length - offset);
            var data = new byte[length];

            Array.Copy(encoded, offset, data, 0, length);

            yield return new ScreenDemoFrameDataMessage
            {
                SubRoomId = context.SubRoomId,
                FrameId = currentFrameId,
                FragmentIndex = i,
                FragmentCount = fragmentCount,
                IsKeyframe = isKeyframe,
                Data = data,
            };
        }
    }

    private async Task SendPumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in sendQueue.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await messageRouter.RouteAsync(message, context.RoomId, identityService.SelfParticipant.Id, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.Log($"Ошибка отправки кадра демонстрации: {ex.Message}", LogLevel.Error);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            sendQueue.Writer.TryComplete();
        }
    }
}
