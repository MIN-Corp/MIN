using System.Runtime.InteropServices;
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
using MIN.ScreenDemo.Services.Contracts.Enums;
using WinRtDevice = Windows.Graphics.DirectX.Direct3D11.IDirect3DDevice;

namespace MIN.ScreenDemo.Capture.Windows;

/// <inheritdoc cref="IScreenCaptureService"/>
public class WindowsGraphicsCaptureService : IScreenCaptureService
{
    private readonly ILoggerProvider logger;

    private ID3D11Device? d3dDevice;
    private ID3D11DeviceContext? d3dContext;
    private WinRtDevice? winrtDevice;
    private ID3D11Texture2D? stagingTexture;
    private uint stagingWidth;
    private uint stagingHeight;
    private GraphicsCaptureItem? captureItem;
    private Direct3D11CaptureFramePool? framePool;
    private GraphicsCaptureSession? session;
    private long lastFrameTimestamp;
    private bool isStarted;

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

        // 1. D3D11 устройство с поддержкой BGRA
        D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport, null, out d3dDevice).CheckError();
        d3dContext = d3dDevice!.ImmediateContext;
        winrtDevice = Direct3D11Helper.CreateDirect3DDevice(d3dDevice);

        // 2. Источник захвата: монитор или окно (из непрозрачного Id источника)
        var handle = new IntPtr(long.Parse(source.Id[2..]));

        captureItem = source.Kind == ScreenDemoSourceKind.Monitor
            ? Direct3D11Helper.CreateCaptureItemForMonitor(handle)
            : Direct3D11Helper.CreateCaptureItemForWindow(handle);

        if (captureItem == null)
        {
            throw new InvalidOperationException($"Не удалось открыть источник захвата '{source.Title}' (окно могло закрыться)");
        }

        // 3. Пул кадров: free-threaded (колбеки вне UI-потока), двойная буферизация, BGRA
        framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            winrtDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            2,
            captureItem.Size);

        framePool.FrameArrived += OnFrameArrived;

        session = framePool.CreateCaptureSession(captureItem);
        session.IsCursorCaptureEnabled = true;
        session.StartCapture();

        isStarted = true;
        lastFrameTimestamp = 0;

        logger.Log($"Захват '{source.Title}' запущен ({captureItem.Size.Width}x{captureItem.Size.Height})");
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (!isStarted)
        {
            return;
        }

        isStarted = false;

        if (framePool != null)
        {
            framePool.FrameArrived -= OnFrameArrived;
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
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => Stop();

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        try
        {
            using var frame = sender.TryGetNextFrame();

            if (frame == null || !isStarted)
            {
                return;
            }

            // Троттлинг до ScreenDemoConstants.Fps: WGC шлёт с частотой монитора (60+ Гц)
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
                var stride = (int)mapped.RowPitch;
                var data = new byte[width * 4 * height];

                if (stride == width * 4)
                {
                    Marshal.Copy(mapped.DataPointer, data, 0, data.Length);
                }
                else
                {
                    for (var y = 0; y < height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(mapped.DataPointer, y * stride), data, y * width * 4, width * 4);
                    }
                }

                lastFrameTimestamp = Environment.TickCount64;

                FrameCaptured?.Invoke(this, new ScreenFrame(width, height, data, lastFrameTimestamp));
            }
            finally
            {
                d3dContext.Unmap(staging, 0);
            }
        }
        catch (Exception ex)
        {
            logger.Log($"Ошибка чтения кадра захвата: {ex.Message}");
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
}
