using System.Threading.Channels;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Services.Contracts.Interfaces.Messaging;
using MIN.Core.SubRooms.Contracts.Models;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;
using MIN.Voice.Messaging;
using MIN.Voice.Services.Contacts.Interfaces;
using MIN.Voice.Services.Contacts.Models;

namespace MIN.Voice.Services;

/// <inheritdoc cref="IVoiceDataTransmitter"/>
public class VoiceDataTransmitter : IVoiceDataTransmitter
{
    private readonly IAudioCaptureService audioCaptureService;
    private readonly IVoiceCodec codec;
    private readonly IVoiceAudioDetector voiceAudioDetector;
    private readonly IMessageRouter messageRouter;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    private Channel<VoiceDataMessage> queue = null!;
    private Channel<byte[]> captureQueue = null!;
    private CancellationTokenSource? sendCts;

    private Guid roomId;
    private int subRoomId;
    private long sequenceNumber;
    private bool isActive;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="VoiceDataTransmitter"/>
    /// </summary>
    public VoiceDataTransmitter(IAudioCaptureService audioCaptureService,
        IVoiceCodec codec,
        IVoiceAudioDetector voiceAudioDetector,
        IMessageRouter messageRouter,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.audioCaptureService = audioCaptureService;
        this.voiceAudioDetector = voiceAudioDetector;
        this.codec = codec;
        this.messageRouter = messageRouter;
        this.identityService = identityService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public void Begin(SubRoomContext subRoomContext)
    {
        if (isActive)
        {
            return;
        }

        roomId = subRoomContext.RoomId;
        subRoomId = subRoomContext.SubRoomId;
        isActive = true;

        captureQueue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        queue = Channel.CreateBounded<VoiceDataMessage>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        sendCts = new CancellationTokenSource();
        _ = ProcessCapturedFramesAsync(sendCts.Token);
        _ = SendPumpAsync(sendCts.Token);

        voiceAudioDetector.Reset();
        audioCaptureService.FrameCaptured += OnFrameCaptured;
    }

    /// <inheritdoc />
    public void End()
    {
        if (!isActive)
        {
            return;
        }

        isActive = false;

        audioCaptureService.FrameCaptured -= OnFrameCaptured;

        sendCts?.Cancel();
        sendCts = null;
        captureQueue.Writer.TryComplete();
        queue.Writer.TryComplete();

        voiceAudioDetector.Reset();
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => End();

    private void OnFrameCaptured(object? sender, AudioFrame e)
    {
        if (!isActive)
        {
            return;
        }

        captureQueue.Writer.TryWrite(e.Data);
    }

    private async Task ProcessCapturedFramesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var data in captureQueue.Reader.ReadAllAsync(cancellationToken))
            {
                if (!voiceAudioDetector.IsVoice(data))
                {
                    continue;
                }

                var currentSequence = Interlocked.Increment(ref sequenceNumber);
                var message = new VoiceDataMessage
                {
                    SubRoomId = subRoomId,
                    SequenceNumber = currentSequence,
                    Codec = codec.Kind,
                    Data = codec.Encode(data),
                };

                queue.Writer.TryWrite(message);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SendPumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in queue.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await messageRouter.RouteAsync(message, roomId, identityService.SelfParticipant.Id, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.Log($"Ошибка отправки голосового кадра: {ex.Message}", LogLevel.Error);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            queue.Writer.TryComplete();
        }
    }
}
