using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;
using MIN.ScreenDemo.Capture.Windows.Internal;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Capture.Windows;

/// <inheritdoc cref="ISystemAudioCaptureService"/>
public class WindowsSystemAudioCaptureService : ISystemAudioCaptureService
{
    private readonly ILoggerProvider logger;
    private readonly object sync = new();

    private ProcessLoopbackAudioEngine? processEngine;
    private WasapiLoopbackAudioCaptureService? fallback;
    private bool isStarted;

    /// <inheritdoc />
    public event Action<ScreenDemoAudioFrame>? FrameCaptured;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="WindowsSystemAudioCaptureService"/>
    /// </summary>
    public WindowsSystemAudioCaptureService(ILoggerProvider logger)
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

            isStarted = true;

            // Основной режим: весь системный микс, кроме нашего процесса (без звука звонка)
            var engine = new ProcessLoopbackAudioEngine(logger, OnFrame);

            if (engine.TryStart())
            {
                processEngine = engine;
                return;
            }

            engine.Dispose();

            // Резерв: старые Windows или сбой активации — захват всего устройства.
            // Звонок при этом попадает в запись (эхо у зрителей)
            logger.Log("Звук демонстрации: резервный режим — захват всего выходного устройства (звук звонка попадёт в запись)", LogLevel.Warning);

            fallback = new WasapiLoopbackAudioCaptureService(logger);
            fallback.FrameCaptured += OnFrame;

            try
            {
                fallback.Start();
            }
            catch (Exception ex)
            {
                logger.Log($"Не удалось запустить захват системного звука: {ex.Message}", LogLevel.Error);
                fallback.FrameCaptured -= OnFrame;
                fallback = null;
            }
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (sync)
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;

            if (processEngine != null)
            {
                processEngine.Stop();
                processEngine.Dispose();
                processEngine = null;
            }

            if (fallback != null)
            {
                fallback.FrameCaptured -= OnFrame;
                fallback.Stop();
                fallback.Dispose();
                fallback = null;
            }
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => Stop();

    private void OnFrame(ScreenDemoAudioFrame frame)
        => FrameCaptured?.Invoke(frame);
}
