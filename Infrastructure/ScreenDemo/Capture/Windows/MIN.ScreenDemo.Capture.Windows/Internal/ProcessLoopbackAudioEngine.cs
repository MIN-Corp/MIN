using System.Runtime.InteropServices;
using MIN.Helpers.Contracts.Interfaces;
using MIN.ScreenDemo.Capture.Windows.Interop;
using MIN.ScreenDemo.Services.Contracts.Constants;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Capture.Windows.Internal;

/// <summary>
/// Захват системного звука с исключением собственного процесса дерева
/// (WASAPI process loopback, Windows 10 2004+). Именно так звонок приложения
/// (голоса других участников) не попадает в запись — зрители не слышат себя.
/// Событийного колбека нет: беззвучные периоды process loopback не сигнализирует,
/// поэтому буфер опрашивается потоком по таймеру
/// </summary>
internal sealed class ProcessLoopbackAudioEngine : IDisposable
{
    /// <summary>
    /// Размер буфера аудио клиента, 100 мс (единицы 100 нс)
    /// </summary>
    private const long BufferDurationHns = 1_000_000;

    private const int PollIntervalMs = 10;

    private readonly ILoggerProvider logger;
    private readonly AudioFrameComposer composer = new();
    private readonly Action<ScreenDemoAudioFrame> raiseFrame;

    private AudioClientInterop.IAudioClient? audioClient;
    private AudioClientInterop.IAudioCaptureClient? captureClient;
    private Thread? pollThread;
    private CancellationTokenSource? pollCts;
    private volatile bool started;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ProcessLoopbackAudioEngine"/>
    /// </summary>
    public ProcessLoopbackAudioEngine(ILoggerProvider logger, Action<ScreenDemoAudioFrame> raiseFrame)
    {
        this.logger = logger;
        this.raiseFrame = raiseFrame;
    }

    /// <summary>
    /// Пытается запустить захват. False — платформа не поддерживает
    /// (Windows &lt; 2004 или ошибка активации), нужен резервный движок
    /// </summary>
    public bool TryStart()
    {
        var formatPtr = IntPtr.Zero;

        try
        {
            audioClient = ActivateProcessLoopback();
            formatPtr = CreateWireFormat();

            audioClient.Initialize(
                AudioClientInterop.ShareModeShared,
                AudioClientInterop.StreamFlagsLoopback,
                BufferDurationHns,
                0,
                formatPtr,
                IntPtr.Zero);

            audioClient = ActivateProcessLoopback();

            var captureIid = AudioClientInterop.IidiAudioCaptureClient;
            audioClient.GetService(ref captureIid, out var capturePtr);
            captureClient = (AudioClientInterop.IAudioCaptureClient)Marshal.GetObjectForIUnknown(capturePtr);
            Marshal.Release(capturePtr);

            audioClient.Start();

            pollCts = new CancellationTokenSource();
            pollThread = new Thread(PollLoop)
            {
                Name = "ScreenDemoProcessLoopbackThread",
                IsBackground = true,
            };
            pollThread.Start(pollCts.Token);

            started = true;
            logger.Log("Захват звука: process loopback с исключением собственного процесса (звонок в запись не попадает)");
            return true;
        }
        catch (Exception ex)
        {
            logger.Log($"Process loopback недоступен: {ex.Message}");
            Cleanup();
            return false;
        }
        finally
        {
            if (formatPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(formatPtr);
            }
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => Stop();

    /// <summary>
    /// Остановить захват
    /// </summary>
    public void Stop()
    {
        if (!started)
        {
            return;
        }

        started = false;

        pollCts?.Cancel();

        try
        {
            audioClient?.Stop();
        }
        catch
        {
            // устройство могло исчезнуть
        }

        Cleanup();
    }

    private void Cleanup()
    {
        captureClient = null;
        audioClient = null;
        pollThread = null;
        pollCts = null;
    }

    /// <summary>
    /// Активация виртуального устройства process loopback с исключением
    /// собственного дерева процессов
    /// </summary>
    private static AudioClientInterop.IAudioClient ActivateProcessLoopback()
    {
        var activationParams = new AudioClientInterop.AudioClientActivationParams
        {
            ActivationType = AudioClientInterop.ActivationTypeProcessLoopback,
            ProcessLoopbackParams = new AudioClientInterop.AudioClientProcessLoopbackParams
            {
                ProcessId = Environment.ProcessId,
                ProcessLoopbackMode = AudioClientInterop.ProcessLoopbackModeExcludeTargetProcessTree,
            },
        };

        var nativeParams = Marshal.AllocHGlobal(Marshal.SizeOf<AudioClientInterop.AudioClientActivationParams>());
        Marshal.StructureToPtr(activationParams, nativeParams, false);

        try
        {
            var propVariant = new AudioClientInterop.ActivationPropVariant
            {
                Vt = AudioClientInterop.VariantTypeBlob,
                CbSize = Marshal.SizeOf<AudioClientInterop.AudioClientActivationParams>(),
                PBlobData = nativeParams,
            };

            var handler = new AudioClientInterop.AudioInterfaceActivationHandler();

            var hr = AudioClientInterop.ActivateAudioInterfaceAsync(
                AudioClientInterop.VirtualProcessLoopbackDevice,
                AudioClientInterop.IidiAudioClient,
                ref propVariant,
                handler);
            Marshal.ThrowExceptionForHR(hr);

            var activated = handler.Activated.Task.GetAwaiter().GetResult();

            try
            {
                return (AudioClientInterop.IAudioClient)Marshal.GetObjectForIUnknown(activated);
            }
            finally
            {
                Marshal.Release(activated);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(nativeParams);
        }
    }

    /// <summary>
    /// Формат запроса: ровно тот, что уходит по проводу — s16le стерео 48 кГц.
    /// WASAPI сам сконвертирует системный микс в этот формат
    /// </summary>
    private static IntPtr CreateWireFormat()
    {
        var format = new AudioClientInterop.WaveFormatEx
        {
            FormatTag = 1, // WAVE_FORMAT_PCM
            Channels = ScreenDemoConstants.AudioChannels,
            SampleRate = ScreenDemoConstants.AudioSampleRate,
            AvgBytesPerSec = ScreenDemoConstants.AudioFrameBytes * (1000 / ScreenDemoConstants.AudioFrameDurationMs),
            BlockAlign = ScreenDemoConstants.AudioChannels * 2,
            BitsPerSample = 16,
            CbSize = 0,
        };

        var native = Marshal.AllocHGlobal(Marshal.SizeOf<AudioClientInterop.WaveFormatEx>());
        Marshal.StructureToPtr(format, native, false);
        return native;
    }

    private void PollLoop(object? state)
    {
        var cancellationToken = (CancellationToken)state!;

        try
        {
            while (!cancellationToken.IsCancellationRequested && started)
            {
                var client = captureClient;

                if (client == null)
                {
                    return;
                }

                // Выгребаем все накопленные пакеты
                while (client.GetNextPacketSize(out var packetFrames) == 0 && packetFrames > 0)
                {
                    if (client.GetBuffer(out var data, out packetFrames, out var flags, out _, out _) != 0)
                    {
                        break;
                    }

                    try
                    {
                        if (packetFrames > 0 && (flags & AudioClientInterop.BufferFlagsSilent) == 0)
                        {
                            var byteCount = (int)packetFrames * 4; // s16 стерео = 4 байта на фрейм
                            var buffer = new byte[byteCount];
                            Marshal.Copy(data, buffer, 0, byteCount);
                            composer.AddS16(buffer, Environment.TickCount64, raiseFrame);
                        }
                        // SILENT-пакеты — нули: тишину композер и так отбрасывает
                    }
                    finally
                    {
                        client.ReleaseBuffer(packetFrames);
                    }
                }

                Thread.Sleep(PollIntervalMs);
            }
        }
        catch (Exception)
        {
            // остановка/освобождение интерфейсов — просто выходим
        }
    }
}
