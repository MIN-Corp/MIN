using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using WinRT;
using WinRT.Interop;
using WinRtDevice = Windows.Graphics.DirectX.Direct3D11.IDirect3DDevice;
using WinRtSurface = Windows.Graphics.DirectX.Direct3D11.IDirect3DSurface;

namespace MIN.ScreenDemo.Capture.Windows.Interop;

/// <summary>
/// Мост между D3D11 (Vortice) и WinRT IDirect3DDevice/ITexture2D для Windows.Graphics.Capture
/// </summary>
public static class Direct3D11Helper
{
    private const string GraphicsCaptureItemRuntimeClassId = "Windows.Graphics.Capture.GraphicsCaptureItem";

    // IID интерфейса GraphicsCaptureItem
    private readonly static Guid graphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    // IID interop-интерфейса активационной фабрики GraphicsCaptureItem
    private readonly static Guid graphicsCaptureItemInteropIid = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");

    // IID IDirect3DDxgiInterfaceAccess
    private readonly static Guid direct3DDxgiInterfaceAccessIid = new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");

    // ВАЖНО: IGraphicsCaptureItemInterop наследуется от IUnknown (см. windows.graphics.capture.interop.h),
    // поэтому InterfaceIsIUnknown. InterfaceIsIInspectable больше не поддерживается CLR .NET 5+
    // — методы на таких интерфейсах бросают "Marshalling as IInspectable is not supported"
    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        /// <summary>
        /// Создать GraphicsCaptureItem для окна
        /// </summary>
        void CreateForWindow(IntPtr window, ref Guid iid, out IntPtr result);

        /// <summary>
        /// Создать GraphicsCaptureItem для монитора
        /// </summary>
        void CreateForMonitor(IntPtr hMonitor, ref Guid iid, out IntPtr result);
    }

    // IDirect3DDxgiInterfaceAccess тоже IUnknown-based — InterfaceIsIUnknown корректен
    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        /// <summary>
        /// Получить нативный интерфейс
        /// </summary>
        void GetInterface(ref Guid iid, out IntPtr ppv);
    }

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern uint CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [DllImport("combase.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string source, int length, out IntPtr hString);

    [DllImport("combase.dll")]
    private static extern void WindowsDeleteString(IntPtr hString);

    [DllImport("combase.dll", PreserveSig = false)]
    private static extern void RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

    /// <summary>
    /// Обернуть Vortice D3D11 устройство в WinRT IDirect3DDevice (нужен для FramePool)
    /// </summary>
    public static WinRtDevice CreateDirect3DDevice(ID3D11Device d3dDevice)
    {
        using var dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>();
        var result = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var winrtPointer);

        if (result != 0)
        {
            throw new InvalidOperationException($"Не удалось создать IDirect3DDevice: 0x{result:X8}");
        }

        try
        {
            return WinRT.MarshalInterface<WinRtDevice>.FromAbi(winrtPointer);
        }
        finally
        {
            Marshal.Release(winrtPointer);
        }
    }

    /// <summary>
    /// Создать GraphicsCaptureItem для окна (через COM-фабрику)
    /// </summary>
    public static GraphicsCaptureItem CreateCaptureItemForWindow(IntPtr window)
    {
        return CreateFromInterop((interop, iid) =>
        {
            interop.CreateForWindow(window, ref iid, out var itemPointer);
            return itemPointer;
        });
    }

    /// <summary>
    /// Создать GraphicsCaptureItem для монитора (через COM-фабрику)
    /// </summary>
    public static GraphicsCaptureItem CreateCaptureItemForMonitor(IntPtr hMonitor)
    {
        return CreateFromInterop((interop, iid) =>
        {
            interop.CreateForMonitor(hMonitor, ref iid, out var itemPointer);
            return itemPointer;
        });
    }

    /// <summary>
    /// Достать нативный D3D11 текстурный указатель из IDirect3DSurface кадра захвата
    /// </summary>
    public static IntPtr GetTexturePointerFromSurface(WinRtSurface surface)
    {
        var surfacePointer = ((IWinRTObject)surface).NativeObject.ThisPtr;

        var accessIid = direct3DDxgiInterfaceAccessIid;
        var hr = Marshal.QueryInterface(surfacePointer, ref accessIid, out var accessPointer);
        Marshal.ThrowExceptionForHR(hr);

        try
        {
            var access = (IDirect3DDxgiInterfaceAccess)Marshal.GetObjectForIUnknown(accessPointer);

            var iid = typeof(ID3D11Texture2D).GUID;
            access.GetInterface(ref iid, out var texturePointer);

            return texturePointer;
        }
        finally
        {
            Marshal.Release(accessPointer);
        }
    }

    private static GraphicsCaptureItem CreateFromInterop(Func<IGraphicsCaptureItemInterop, Guid, IntPtr> create)
    {
        WindowsCreateString(GraphicsCaptureItemRuntimeClassId, GraphicsCaptureItemRuntimeClassId.Length, out var hString);

        try
        {
            var interopIid = graphicsCaptureItemInteropIid;
            RoGetActivationFactory(hString, ref interopIid, out var interopPointer);

            try
            {
                // IUnknown-based ComImport — встроенный RCW-механизм .NET это поддерживает
                var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(interopPointer);

                var itemIid = graphicsCaptureItemIid;
                var itemPointer = create(interop, itemIid);

                try
                {
                    return GraphicsCaptureItem.FromAbi(itemPointer);
                }
                finally
                {
                    Marshal.Release(itemPointer);
                }
            }
            finally
            {
                Marshal.Release(interopPointer);
            }
        }
        finally
        {
            WindowsDeleteString(hString);
        }
    }
}
