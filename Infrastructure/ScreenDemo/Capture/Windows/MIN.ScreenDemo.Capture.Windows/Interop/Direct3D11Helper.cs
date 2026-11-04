using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using WinRtDevice = Windows.Graphics.DirectX.Direct3D11.IDirect3DDevice;
using WinRtSurface = Windows.Graphics.DirectX.Direct3D11.IDirect3DSurface;

namespace MIN.ScreenDemo.Capture.Windows.Interop;

/// <summary>
/// Мост между D3D11 (Vortice) и WinRT IDirect3DDevice/ITexture2D для Windows.Graphics.Capture.
/// Interop-интерфейсы вызываются напрямую через vtable, БЕЗ встроенных RCW:
/// пул кадров циклически переиспользует одни и те же COM-объекты поверхностей,
/// а кеш RCW привязывает обёртку к потоку создания — на другом потоке колбека
/// FrameArrived это даёт RPC_E_WRONG_THREAD
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

    // vtable IGraphicsCaptureItemInterop: IUnknown (слоты 0-2) + CreateForWindow (3) + CreateForMonitor (4)
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateItemDelegate(IntPtr thisPtr, IntPtr handle, ref Guid iid, out IntPtr result);

    // vtable IDirect3DDxgiInterfaceAccess: IUnknown (слоты 0-2) + GetInterface (3)
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetInterfaceDelegate(IntPtr thisPtr, ref Guid iid, out IntPtr ppv);

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
    /// Создать GraphicsCaptureItem для окна
    /// </summary>
    public static GraphicsCaptureItem CreateCaptureItemForWindow(IntPtr window)
        => CreateFromInterop(3, window);

    /// <summary>
    /// Создать GraphicsCaptureItem для монитора
    /// </summary>
    public static GraphicsCaptureItem CreateCaptureItemForMonitor(IntPtr hMonitor)
        => CreateFromInterop(4, hMonitor);

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
            var vtable = Marshal.ReadIntPtr(accessPointer);
            var methodPointer = Marshal.ReadIntPtr(vtable, 3 * IntPtr.Size);
            var getInterface = Marshal.GetDelegateForFunctionPointer<GetInterfaceDelegate>(methodPointer);

            var iid = typeof(ID3D11Texture2D).GUID;
            var result = getInterface(accessPointer, ref iid, out var texturePointer);
            Marshal.ThrowExceptionForHR(result);

            return texturePointer;
        }
        finally
        {
            Marshal.Release(accessPointer);
        }
    }

    private static GraphicsCaptureItem CreateFromInterop(int methodSlot, IntPtr handle)
    {
        WindowsCreateString(GraphicsCaptureItemRuntimeClassId, GraphicsCaptureItemRuntimeClassId.Length, out var hString);

        try
        {
            var interopIid = graphicsCaptureItemInteropIid;
            RoGetActivationFactory(hString, ref interopIid, out var factoryPointer);

            try
            {
                var vtable = Marshal.ReadIntPtr(factoryPointer);
                var methodPointer = Marshal.ReadIntPtr(vtable, methodSlot * IntPtr.Size);
                var createMethod = Marshal.GetDelegateForFunctionPointer<CreateItemDelegate>(methodPointer);

                var itemIid = graphicsCaptureItemIid;
                var result = createMethod(factoryPointer, handle, ref itemIid, out var itemPointer);
                Marshal.ThrowExceptionForHR(result);

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
                Marshal.Release(factoryPointer);
            }
        }
        finally
        {
            WindowsDeleteString(hString);
        }
    }
}
