using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Windows.Graphics;
using MIN.ScreenDemo.Capture.Windows.Interop;
using MIN.ScreenDemo.Services.Contracts.Constants;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;
using MIN.ScreenDemo.Services.Contracts.Enums;
using WinRtDevice = Windows.Graphics.DirectX.Direct3D11.IDirect3DDevice;

namespace MIN.ScreenDemo.Capture.Windows;

/// <inheritdoc cref="IScreenCaptureService"/>
public class WindowsGraphicsCaptureService : IScreenCaptureService
{
    private readonly ILoggerProvider logger;

    private WinRtDevice? winrtDevice;
    private ID3D11Device? d3dDevice;
    private ID3D11DeviceContext? d3dContext;
    private ID3D11Texture2D? stagingTexture;
    private uint stagingWidth;
    private uint stagingHeight;
    private GraphicsCaptureItem? captureItem;
    private Direct3D11CaptureFramePool? framePool;
    private GraphicsCaptureSession? session;
    private SizeInt32 poolSize;
    private long lastFrameTimestamp;
    private volatile bool isStarted;

    private Thread? ownerThread;
    private BlockingCollection<Action>? ownerQueue;

    /// <inheritdoc />
    public event EventHandler<ScreenFrame>? FrameCaptured;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="WindowsGraphicsCaptureService"/>
    /// </summary>
    public WindowsGraphicsCaptureService(ILoggerProvider logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public void Start(ScreenSourceInfo source)
    {
        if (isStarted)
        {
            return;
        }

        if (!GraphicsCaptureSession.IsSupported())
        {
            throw new PlatformNotSupportedException("Windows.Graphics.Capture не поддерживается на этой системе (нужна Windows 10 1903+)");
        }

        ownerQueue = new BlockingCollection<Action>(new ConcurrentQueue<Action>());
        ownerThread = new Thread(OwnerLoop)
        {
            Name = "ScreenDemoCaptureThread",
            IsBackground = true,
        };
        ownerThread.Start();

        // Инициализация выполняется на потоке-владельце: устройство, пул и сессия
        // должны жить и вызываться с одного потока. Исключение пробрасывается сюда
        var setup = new TaskCompletionSource();
        ownerQueue.Add(() =>
        {
            try
            {
                StartInternal(source);
                setup.SetResult();
            }
            catch (Exception ex)
            {
                setup.SetException(ex);
            }
        });
        setup.Task.GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (!isStarted)
        {
            return;
        }

        isStarted = false;

        // Разборка тоже на потоке-владельце (fire-and-forget: поток завершится сам)
        ownerQueue?.TryAdd(StopInternal);
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => Stop();

    private void StartInternal(ScreenSourceInfo source)
    {
        // 1. D3D11 устройство с поддержкой BGRA
        D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport, null, out d3dDevice).CheckError();
        d3dContext = d3dDevice!.ImmediateContext;
        winrtDevice = Direct3D11Helper.CreateDirect3DDevice(d3dDevice);

        // 2. Источник захвата: монитор или окно
        var handle = new IntPtr(long.Parse(source.Id[2..]));

        captureItem = source.Kind == ScreenDemoSourceKind.Monitor
            ? Direct3D11Helper.CreateCaptureItemForMonitor(handle)
            : Direct3D11Helper.CreateCaptureItemForWindow(handle);

        if (captureItem == null)
        {
            throw new InvalidOperationException($"Не удалось открыть источник захвата '{source.Title}' (окно могло закрыться)");
        }

        captureItem.Closed += OnCaptureItemClosed;

        // 3. Пул кадров создаётся и живёт на потоке-владельце
        framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            winrtDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            2,
            captureItem.Size);
        poolSize = captureItem.Size;

        framePool.FrameArrived += OnFrameArrived;

        session = framePool.CreateCaptureSession(captureItem);
        session.IsCursorCaptureEnabled = true;
        session.StartCapture();

        isStarted = true;
        lastFrameTimestamp = 0;

        logger.Log($"Захват '{source.Title}' запущен ({captureItem.Size.Width}x{captureItem.Size.Height})");
    }

    private void StopInternal()
    {
        if (framePool != null)
        {
            framePool.FrameArrived -= OnFrameArrived;
        }

        if (captureItem != null)
        {
            captureItem.Closed -= OnCaptureItemClosed;
        }

        session?.Dispose();
        framePool?.Dispose();
        captureItem = null;

        stagingTexture?.Dispose();
        stagingTexture = null;

        d3dContext?.Dispose();
        d3dDevice?.Dispose();
        winrtDevice = null;

        logger.Log("Захват остановлен");

        // После разбора задач больше не будет — поток завершится
        ownerQueue?.CompleteAdding();
    }

    /// <summary>
    /// Поток-владелец захвата: выполняет все задачи по очереди
    /// </summary>
    private void OwnerLoop()
    {
        try
        {
            foreach (var action in ownerQueue!.GetConsumingEnumerable())
            {
                action();
            }
        }
        catch (Exception ex)
        {
            logger.Log($"Поток захвата завершился с ошибкой: {ex}", LogLevel.Error);
        }
        finally
        {
            ownerQueue?.Dispose();
            ownerQueue = null;
            ownerThread = null;
        }
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        // Ничего не трогаем на потоке колбека — только ставим задачу владельцу
        ownerQueue?.TryAdd(ProcessNewFrames);
    }

    /// <summary>
    /// Обработка кадров — выполняется ТОЛЬКО на потоке-владельце
    /// </summary>
    private void ProcessNewFrames()
    {
        if (!isStarted || framePool == null)
        {
            return;
        }

        // Выгребаем накопившиеся кадры, оставляем самый свежий
        Direct3D11CaptureFrame? frame = null;

        try
        {
            while (framePool.TryGetNextFrame() is { } next)
            {
                frame?.Dispose();
                frame = next;
            }

            if (frame == null)
            {
                return;
            }

            // Ресайз захваченного окна (или смена разрешения монитора):
            // Recreate обязан вызываться на потоке создания пула — здесь это выполняется
            if (frame.ContentSize.Width != poolSize.Width || frame.ContentSize.Height != poolSize.Height)
            {
                framePool.Recreate(winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, frame.ContentSize);
                poolSize = frame.ContentSize;

                stagingTexture?.Dispose();
                stagingTexture = null;

                logger.Log($"Размер захвата изменился: {poolSize.Width}x{poolSize.Height}");

                return;
            }

            // Троттлинг до ScreenDemoConstants.Fps
            var now = Environment.TickCount64;

            if (now - lastFrameTimestamp < ScreenDemoConstants.FrameIntervalMs)
            {
                return;
            }

            var surface = frame.Surface;
            var texturePointer = Direct3D11Helper.GetTexturePointerFromSurface(surface);

            using var texture = new ID3D11Texture2D(texturePointer);
            var description = texture.Description;
            var width = (int)description.Width;
            var height = (int)description.Height;

            EnsureStagingTexture(description.Width, description.Height);
            var staging = stagingTexture!;

            d3dContext!.CopyResource(staging, texture);

            var mapped = d3dContext.Map(staging, 0);

            try
            {
                var pixelByteCount = width * 4 * height;
                var frameBytes = ArrayPool<byte>.Shared.Rent(pixelByteCount);
                var stride = (int)mapped.RowPitch;

                if (stride == width * 4)
                {
                    Marshal.Copy(mapped.DataPointer, frameBytes, 0, pixelByteCount);
                }
                else
                {
                    for (var y = 0; y < height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(mapped.DataPointer, y * stride), frameBytes, y * width * 4, width * 4);
                    }
                }

                try
                {
                    lastFrameTimestamp = Environment.TickCount64;
                    FrameCaptured?.Invoke(this, new ScreenFrame(width, height, frameBytes, lastFrameTimestamp));
                }
                finally
                {
                    d3dContext.Unmap(staging, 0);
                }
            }
            finally
            {
                d3dContext.Unmap(staging, 0);
            }
        }
        catch (Exception ex)
        {
            logger.Log($"Ошибка чтения кадра захвата: {ex}", LogLevel.Error);
        }
        finally
        {
            frame?.Dispose();
        }
    }

    private void EnsureStagingTexture(uint width, uint height)
    {
        if (stagingTexture != null && stagingWidth == width && stagingHeight == height)
        {
            return;
        }

        stagingTexture?.Dispose();

        var description = new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
        };

        stagingTexture = d3dDevice!.CreateTexture2D(description);
        stagingWidth = width;
        stagingHeight = height;
    }

    private void OnCaptureItemClosed(GraphicsCaptureItem sender, object args)
    {
        logger.Log("Источник захвата закрыт (окно закрыто или монитор отключён)");
        Stop();
    }
}
