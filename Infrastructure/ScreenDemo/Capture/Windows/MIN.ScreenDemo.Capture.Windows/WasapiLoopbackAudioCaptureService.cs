using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;
using MIN.ScreenDemo.Capture.Windows.Internal;
using MIN.ScreenDemo.Services.Contracts.Constants;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MIN.ScreenDemo.Capture.Windows;

/// <inheritdoc cref="ISystemAudioCaptureService"/>
public class WasapiLoopbackAudioCaptureService : ISystemAudioCaptureService
{
    /// <summary>
    /// Размер блока чтения из конвейера: 50 мс стерео 48 кГц (48000 * 0.05 * 2)
    /// </summary>
    private const int ReadFloatCount = ScreenDemoConstants.AudioSampleRate
        * ScreenDemoConstants.AudioChannels * 50 / 1000;

    private readonly ILoggerProvider logger;
    private readonly object sync = new();

    private WasapiLoopbackCapture? capture;
    private BufferedWaveProvider? rawBuffer;
    private ISampleProvider? samplePipeline;
    private readonly AudioFrameComposer composer = new();
    private int sourceChannels;
    private CancellationTokenSource? dispatchCts;
    private AutoResetEvent? dataAvailableSignal;
    private Thread? dispatchThread;
    private volatile bool isStarted;

    /// <inheritdoc />
    public event Action<ScreenDemoAudioFrame>? FrameCaptured;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="WasapiLoopbackAudioCaptureService"/>
    /// </summary>
    public WasapiLoopbackAudioCaptureService(ILoggerProvider logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public void Start()
    {
        lock (sync)
        {
            if (isStarted)
            {
                return;
            }

            try
            {
                // Выходное устройство по умолчанию — системный микс всех приложений
                capture = new WasapiLoopbackCapture();
                sourceChannels = capture.WaveFormat.Channels;

                rawBuffer = new BufferedWaveProvider(capture.WaveFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(1), // защита от переполнения при лагах
                    DiscardOnBufferOverflow = true,
                };
                samplePipeline = BuildPipeline(rawBuffer, capture.WaveFormat);

                dataAvailableSignal = new AutoResetEvent(false);
                dispatchCts = new CancellationTokenSource();

                dispatchThread = new Thread(DispatchLoop)
                {
                    Name = "ScreenDemoAudioDispatchThread",
                    IsBackground = true,
                };
                dispatchThread.Start(dispatchCts.Token);

                capture.DataAvailable += OnDataAvailable;
                capture.StartRecording();

                isStarted = true;
                logger.Log("Захват системного звука запущен");
            }
            catch (Exception ex)
            {
                // Нет устройства / WASAPI недоступен — демонстрация продолжится без звука
                logger.Log($"Не удалось запустить захват системного звука: {ex.Message}", LogLevel.Error);
                CleanupLocked();
            }
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        WasapiLoopbackCapture? captureToStop;

        lock (sync)
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            captureToStop = capture;
            CleanupLocked();
        }

        // StopRecording/Dispose присоединяются к внутреннему потоку NAudio — только вне lock,
        // чтобы подписчики кадра не могли встать в очередь за ним
        if (captureToStop == null)
        {
            return;
        }

        try
        {
            captureToStop.StopRecording();
        }
        catch
        {
            // устройство могло исчезнуть
        }

        captureToStop.Dispose();
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => Stop();

    // ===== Внутреннее =====

    /// <summary>
    /// Сырой mix-формат устройства → float 48 кГц (дальше конвертируем в стерео s16 сами)
    /// </summary>
    private static ISampleProvider BuildPipeline(IWaveProvider source, WaveFormat sourceFormat)
    {
        var pipeline = source.ToSampleProvider();

        if (sourceFormat.SampleRate != ScreenDemoConstants.AudioSampleRate)
        {
            pipeline = new WdlResamplingSampleProvider(pipeline, ScreenDemoConstants.AudioSampleRate);
        }

        return pipeline;
    }

    /// <summary>Сброс всего состояния под lock (владение вызовом — у Start/Stop)</summary>
    private void CleanupLocked()
    {
        if (capture != null)
        {
            capture.DataAvailable -= OnDataAvailable;
        }

        capture = null;
        rawBuffer = null;
        samplePipeline = null;

        dispatchCts?.Cancel();
        dispatchCts = null;
        dataAvailableSignal?.Set();
        dataAvailableSignal?.Dispose();
        dataAvailableSignal = null;
        dispatchThread = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Только пушим сырые байты в буфер — конвертация на потоке диспетчеризации
        var buffer = rawBuffer;

        if (buffer == null)
        {
            return;
        }

        buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
        dataAvailableSignal?.Set();
    }

    /// <summary>
    /// Поток диспетчеризации: вычитывает из буфера сырые данные, конвертирует в
    /// s16le стерео, накапливает до полного 20-мс кадра, отфильтровывает тишину
    /// и публикует готовые кадры
    /// </summary>
    private void DispatchLoop(object? state)
    {
        var cancellationToken = (CancellationToken)state!;
        // Вычитка произвольного размера — конвертер и аккумулятор режут на кадры сами
        var readBuffer = new float[ReadFloatCount];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                dataAvailableSignal?.WaitOne(15); // таймаут — страховка от пропущенного Set

                if (!isStarted)
                {
                    return;
                }

                var pipeline = samplePipeline;
                var buffer = rawBuffer;

                if (pipeline == null || buffer == null)
                {
                    return;
                }

                if (buffer.BufferedBytes <= 0)
                {
                    continue;
                }

                var read = pipeline.Read(readBuffer, 0, readBuffer.Length);

                if (read > 0)
                {
                    composer.AddFloats(
                        readBuffer,
                        read,
                        sourceChannels,
                        Environment.TickCount64,
                        frame => FrameCaptured?.Invoke(frame));
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // сигнал/буфер освобождены во время остановки — просто выходим
        }
        catch (Exception ex)
        {
            logger.Log($"Поток диспетчеризации звука завершился с ошибкой: {ex}", LogLevel.Error);
        }
    }
}
