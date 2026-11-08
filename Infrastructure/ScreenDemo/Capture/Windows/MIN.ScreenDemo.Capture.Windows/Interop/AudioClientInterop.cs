using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace MIN.ScreenDemo.Capture.Windows.Interop;

/// <summary>
/// Манипуляции WASAPI для process loopback (захват звука с исключением
/// собственного процесса дерева). Доступно с Windows 10 2004 (build 19041)
/// </summary>
static internal partial class AudioClientInterop
{
    /// <summary>
    /// Виртуальное устройство process loopback (VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK)
    /// </summary>
    public const string VirtualProcessLoopbackDevice = @"VAD\Process_Loopback";

    /// <summary>AUDCLNT_SHAREMODE_SHARED</summary>
    public const int ShareModeShared = 0;

    /// <summary>AUDCLNT_STREAMFLAGS_LOOPBACK</summary>
    public const long StreamFlagsLoopback = 0x00020000;

    /// <summary>AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK</summary>
    public const int ActivationTypeProcessLoopback = 1;

    /// <summary>PROCESS_LOOPBACK_MODE_INCLUDE_TARGET_PROCESS_TREE</summary>
    public const int ProcessLoopbackModeIncludeTargetProcessTree = 0;

    /// <summary>PROCESS_LOOPBACK_MODE_EXCLUDE_TARGET_PROCESS_TREE</summary>
    public const int ProcessLoopbackModeExcludeTargetProcessTree = 1;

    /// <summary>AUDCLNT_BUFFERFLAGS_SILENT</summary>
    public const int BufferFlagsSilent = 0x1;

    /// <summary>VT_BLOB</summary>
    public const short VariantTypeBlob = 65;

    public readonly static Guid IidiAudioClient = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    public readonly static Guid IidiAudioCaptureClient = new("C8ADBD64-E71E-48A0-A4DE-185C395CD317");

    [StructLayout(LayoutKind.Sequential)]
    public struct AudioClientProcessLoopbackParams
    {
        public int ProcessId;
        public int ProcessLoopbackMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AudioClientActivationParams
    {
        public int ActivationType;
        public AudioClientProcessLoopbackParams ProcessLoopbackParams;
    }

    /// <summary>
    /// PROPVARIANT с VT_BLOB, внутрь которого упаковывается AudioClientActivationParams.
    /// Раскладка соответствует x64 PROPVARIANT: vt@0, резервы@2..8, cbSize@8, pBlobData@16
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ActivationPropVariant
    {
        public short Vt;
        public short WReserved1;
        public short WReserved2;
        public short WReserved3;
        public int CbSize;
        public IntPtr PBlobData;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct WaveFormatEx
    {
        public short FormatTag;      // WAVE_FORMAT_PCM = 1
        public short Channels;
        public int SampleRate;
        public int AvgBytesPerSec;
        public short BlockAlign;
        public short BitsPerSample;
        public short CbSize;         // 0 для PCM без расширения
    }

    /// <summary>
    /// Активация аудио-интерфейса. Асинхронная: результат приходит
    /// в <see cref="IActivateAudioInterfaceCompletionHandler.Completed"/>
    /// </summary>
    [LibraryImport("mmdevapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int ActivateAudioInterfaceAsync(
        string deviceInterfacePath,
        in Guid interfaceId,
        ref ActivationPropVariant activationParams,
        IActivateAudioInterfaceCompletionHandler handler);

    /// <summary>
    /// Обратный вызов активации. Должен быть agile (IAgileObject):
    /// Completed приходит на потоковом пуле
    /// </summary>
    [GeneratedComInterface]
    [Guid("72A22DF6-92CA-4BC6-8BF2-75F895228E07")]
    public partial interface IActivateAudioInterfaceCompletionHandler
    {
        /// <summary>
        /// Вызывается по завершении активации
        /// </summary>
        [PreserveSig]
        int Completed(IActivateAudioInterfaceAsyncOperation operation);
    }

    [GeneratedComInterface]
    [Guid("72A22DF7-92CA-4BC6-8BF2-75F895228E07")]
    public partial interface IActivateAudioInterfaceAsyncOperation
    {
        /// <summary>
        /// Возвращает HRESULT активации и запрошенный интерфейс (IUnknown*)
        /// </summary>
        [PreserveSig]
        int GetActivateResult(out int activateResult, out IntPtr activatedInterface);
    }

    /// <summary>
    /// Маркер agile-объекта: активация приходит с потокового пула,
    /// обработчик не должен иметь apartment-привязки
    /// </summary>
    [GeneratedComInterface]
    [Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA54F5B1")]
    public partial interface IAgileObject
    {
    }

    /// <summary>
    /// Реализация обработчика активации: Completed кладёт результат в TCS,
    /// активирующая сторона ждёт на задаче
    /// </summary>
    [GeneratedComClass]
    public sealed partial class AudioInterfaceActivationHandler :
        IActivateAudioInterfaceCompletionHandler, IAgileObject
    {
        /// <summary>
        /// Завершается IUnknown* активированного интерфейса или исключением при ошибке
        /// </summary>
        public TaskCompletionSource<IntPtr> Activated { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public int Completed(IActivateAudioInterfaceAsyncOperation operation)
        {
            var handlerResult = operation.GetActivateResult(out var activateResult, out var activated);

            if (handlerResult < 0)
            {
                Activated.TrySetException(
                    new InvalidOperationException($"GetActivateResult: HRESULT 0x{handlerResult:X8}"));
                return handlerResult;
            }

            if (activateResult < 0)
            {
                Activated.TrySetException(
                    new InvalidOperationException($"Активация не удалась: HRESULT 0x{activateResult:X8}"));
                return activateResult;
            }

            Activated.TrySetResult(activated);
            return 0;
        }
    }

    /// <summary>
    /// Клиент аудио (вызовы только через классический COM-interop RCW)
    /// </summary>
    [ComImport]
    [Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioClient
    {
        void Initialize(int shareMode, long streamFlags, long bufferDuration,
            long periodicity, IntPtr format, IntPtr audioSessionGuid);

        void GetBufferSize(out uint bufferFrames);

        void GetStreamLatency(out long latency);

        void GetCurrentPadding(out uint currentPadding);

        void IsFormatSupported(int shareMode, IntPtr format, IntPtr closestMatch);

        void GetMixFormat(out IntPtr format);

        void GetDevicePeriod(out long defaultPeriod, out long minPeriod);

        void Start();

        void Stop();

        void Reset();

        void SetEventHandle(IntPtr eventHandle);

        void GetService(ref Guid interfaceId, out IntPtr service);
    }

    /// <summary>
    /// Клиент захвата (PreserveSig: ошибки читаем как коды, не как исключения)
    /// </summary>
    [ComImport]
    [Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioCaptureClient
    {
        [PreserveSig]
        int GetBuffer(out IntPtr data, out uint frames, out int flags,
            out long devicePosition, out long qpcPosition);

        [PreserveSig]
        int ReleaseBuffer(uint frames);

        [PreserveSig]
        int GetNextPacketSize(out uint frames);
    }
}
