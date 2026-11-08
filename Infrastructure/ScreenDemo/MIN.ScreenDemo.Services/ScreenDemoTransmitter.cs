using System.Buffers;
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
    private readonly ISystemAudioCaptureService systemAudioCaptureService;
    private readonly IScreenVideoCodec codec;
    private readonly IMessageRouter messageRouter;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    private Channel<ScreenFrame> captureQueue = null!;
    private Channel<ScreenDemoFrameDataMessage> sendQueue = null!;
    private Channel<ScreenDemoAudioDataMessage> audioQueue = null!;
    private CancellationTokenSource? sendCts;

    private SubRoomContext context;
    private long frameId;
    private long audioSequenceNumber;
    private long lastFrameTimestamp;
    private int lastWidth;
    private int lastHeight;
    private volatile bool forceKeyframe;
    private bool isActive;
    private bool withSound;

    private readonly object viewersGate = new();
    private readonly HashSet<Guid> viewers = [];

    private readonly object audioGate = new();
    private bool audioCaptureRunning;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoTransmitter"/>
    /// </summary>
    public ScreenDemoTransmitter(IScreenCaptureService screenCaptureService,
        ISystemAudioCaptureService systemAudioCaptureService,
        IScreenVideoCodec codec,
        IMessageRouter messageRouter,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.screenCaptureService = screenCaptureService;
        this.systemAudioCaptureService = systemAudioCaptureService;
        this.codec = codec;
        this.messageRouter = messageRouter;
        this.identityService = identityService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler<ScreenFrame>? FramePreview;

    void IScreenDemoTransmitter.Begin(SubRoomContext subRoomContext, ScreenSourceInfo source, bool withSound)
    {
        if (isActive)
        {
            return;
        }

        context = subRoomContext;
        isActive = true;
        frameId = 0;
        audioSequenceNumber = 0;
        lastFrameTimestamp = 0;
        lastWidth = 0;
        lastHeight = 0;
        forceKeyframe = true;
        this.withSound = withSound;

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

        audioQueue = Channel.CreateBounded<ScreenDemoAudioDataMessage>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        sendCts = new CancellationTokenSource();
        _ = ProcessCapturedFramesAsync(sendCts.Token);
        _ = SendPumpAsync(sendCts.Token);
        _ = AudioSendPumpAsync(sendCts.Token);

        screenCaptureService.FrameCaptured += OnFrameCaptured;
        screenCaptureService.Start(source);

        // Звук не захватываем сразу: начнём, когда придёт первый зритель
        if (withSound)
        {
            logger.Log("Демонстрация со звуком: захват начнётся с появлением зрителя");
        }
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

        EnsureAudioCaptureStarted();
    }

    void IScreenDemoTransmitter.RemoveViewer(Guid participantId)
    {
        bool lastViewerLeft;

        lock (viewersGate)
        {
            viewers.Remove(participantId);
            lastViewerLeft = viewers.Count == 0;
        }

        if (lastViewerLeft)
        {
            StopAudioCapture();
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

        StopAudioCapture();

        screenCaptureService.FrameCaptured -= OnFrameCaptured;
        screenCaptureService.Stop();

        sendCts?.Cancel();
        sendCts = null;
        captureQueue.Writer.TryComplete();
        sendQueue.Writer.TryComplete();
        audioQueue.Writer.TryComplete();

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

    /// <summary>
    /// Запускает захват системного звука, если он ещё не идёт.
    /// Захват живёт только пока есть зрители: записывать не для кого
    /// </summary>
    private void EnsureAudioCaptureStarted()
    {
        if (!withSound || !isActive)
        {
            return;
        }

        lock (audioGate)
        {
            if (audioCaptureRunning)
            {
                return;
            }

            audioCaptureRunning = true;
        }

        try
        {
            systemAudioCaptureService.FrameCaptured += OnAudioFrameCaptured;
            systemAudioCaptureService.Start();
            logger.Log("Захват системного звука начат (появился зритель)");
        }
        catch (Exception ex)
        {
            logger.Log($"Звук демонстрации недоступен: {ex.Message}", LogLevel.Warning);

            lock (audioGate)
            {
                audioCaptureRunning = false;
            }

            systemAudioCaptureService.FrameCaptured -= OnAudioFrameCaptured;
        }
    }

    /// <summary>
    /// Останавливает захват системного звука, если он идёт
    /// </summary>
    private void StopAudioCapture()
    {
        lock (audioGate)
        {
            if (!audioCaptureRunning)
            {
                return;
            }

            audioCaptureRunning = false;
        }

        systemAudioCaptureService.FrameCaptured -= OnAudioFrameCaptured;
        systemAudioCaptureService.Stop();
    }

    private void OnAudioFrameCaptured(object? sender, ScreenDemoAudioFrame frame)
    {
        if (!isActive || !HasViewers)
        {
            return;
        }

        // Монотонный счётчик на всё время демонстрации: пауза между волнами
        // зрителей (захвата нет) не сбрасывает нумерацию
        var sequenceNumber = Interlocked.Increment(ref audioSequenceNumber);

        audioQueue.Writer.TryWrite(new ScreenDemoAudioDataMessage
        {
            SubRoomId = context.SubRoomId,
            SequenceNumber = sequenceNumber,
            TimestampMs = frame.TimestampMs,
            Data = frame.Data,
        });
    }

    private async Task AudioSendPumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in audioQueue.Reader.ReadAllAsync(cancellationToken))
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
                    logger.Log($"Ошибка отправки звука демонстрации: {ex.Message}", LogLevel.Error);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            audioQueue.Writer.TryComplete();
        }
    }

    // ===== Видео (без изменений) =====

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
                FramePreview?.Invoke(this, frame);
                ProcessFrame(frame);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ProcessFrame(ScreenFrame frame)
    {
        ScreenFrame? scaled = null;

        try
        {
            // Троттлинг до ScreenDemoConstants.Fps
            if (frame.TimestampMs - lastFrameTimestamp < ScreenDemoConstants.FrameIntervalMs)
            {
                return;
            }

            // Зрителей нет — демонстрация только локальная, кодирование не тратим
            if (!HasViewers)
            {
                return;
            }

            scaled = ScreenFrameScaler.Downscale(frame, ScreenDemoConstants.MaxFrameHeight);

            if (scaled.Width != lastWidth || scaled.Height != lastHeight)
            {
                logger.Log($"Размер потока демонстрации: {scaled.Width}x{scaled.Height}");
                lastWidth = scaled.Width;
                lastHeight = scaled.Height;
                forceKeyframe = true;
            }

            if (forceKeyframe)
            {
                codec.Configure(scaled.Width, scaled.Height, ScreenDemoConstants.TargetBitrateKbps);
                forceKeyframe = false;
            }

            var currentFrameId = Interlocked.Increment(ref frameId);
            var encoded = codec.Encode(scaled, out var isKeyframe);

            lastFrameTimestamp = frame.TimestampMs;

            if (encoded.Length == 0)
            {
                return;
            }

            foreach (var fragment in FragmentFrame(currentFrameId, isKeyframe, scaled.Width, scaled.Height, encoded))
            {
                sendQueue.Writer.TryWrite(fragment);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(frame.Data);

            if (scaled != null && !ReferenceEquals(scaled, frame))
            {
                ArrayPool<byte>.Shared.Return(scaled.Data);
            }
        }
    }

    private IEnumerable<ScreenDemoFrameDataMessage> FragmentFrame(long currentFrameId, bool isKeyframe, int width, int height, byte[] encoded)
    {
        var fragmentCount = (int)Math.Ceiling(encoded.Length / (double)ScreenDemoConstants.FragmentTargetSize);

        if (fragmentCount > ScreenDemoConstants.MaxFragmentsPerFrame)
        {
            logger.Log($"Кадр {currentFrameId} не влез в {ScreenDemoConstants.MaxFragmentsPerFrame} фрагментов ({encoded.Length} байт) — отброшен",
                LogLevel.Warning);
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
                Width = width,
                Height = height,
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
