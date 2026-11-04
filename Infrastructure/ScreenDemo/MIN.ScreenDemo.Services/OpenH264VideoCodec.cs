using System.Runtime.InteropServices;
using H264Sharp;
using MIN.ScreenDemo.Services.Contracts.Constants;
using MIN.ScreenDemo.Services.Contracts.Enums;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Services;

/// <inheritdoc cref="IScreenVideoCodec"/>
public class OpenH264VideoCodec : IScreenVideoCodec
{
    private readonly H264Encoder encoder = new();
    private readonly H264Decoder decoder = new();
    private bool decoderInitialized;

    private RgbImage? decodeOutput;

    ScreenVideoCodecKind IScreenVideoCodec.Kind => ScreenVideoCodecKind.H264;

    void IScreenVideoCodec.Configure(int width, int height, int targetBitrateKbps)
    {
        // Пересоздание энкодера гарантирует ключевой (IDR) кадр на следующем Encode
        // и подхватывает новый размер при изменении размера окна. Вызывается транмиттером
        // на первом кадре, при входе зрителя, при изменении размера и раз в KeyframeIntervalFrames
        encoder.Initialize(width, height, targetBitrateKbps * 1000, ScreenDemoConstants.Fps, ConfigType.CameraCaptureAdvanced);

        // Периодические IDR на стороне кодека — восстановление зрителя после потерь
        encoder.SetOption(ENCODER_OPTION.ENCODER_OPTION_IDR_INTERVAL, ScreenDemoConstants.KeyframeIntervalFrames);
    }

    byte[] IScreenVideoCodec.Encode(ScreenFrame frame, out bool isKeyframe)
    {
        isKeyframe = false;

        // Контейнер ссылается на память кадра без копирования (конвертация BGRA->I420
        // происходит внутри Encode, SIMD-конвертером)
        var input = new RgbImage(ImageFormat.Bgra, frame.Width, frame.Height, frame.Width * 4, frame.Data, 0, frame.Width * 4 * frame.Height);

        if (!encoder.Encode(input, out var encodedFrames) || encodedFrames.Length == 0)
        {
            return []; // кадр пропущен rate-control'ом
        }

        // EncodedData ссылается на эфемерную нативную память — забираем байты сразу.
        // На IDR кадрах фрагментов может быть несколько (первый — метаданные),
        // GetAllBytes склеивает их в один Annex-B поток
        var annexB = encodedFrames.GetAllBytes();

        isKeyframe = ContainsIdrNal(annexB); // надёжнее, чем API-флаги: по NAL-заголовкам

        return annexB;
    }

    bool IScreenVideoCodec.Decode(byte[] encoded, int expectedWidth, int expectedHeight, byte[] targetBuffer)
    {
        if (expectedWidth is < 1 or > 4096 || expectedHeight is < 1 or > 2160)
        {
            return false;
        }

        if (!decoderInitialized)
        {
            decoder.Initialize();
            decoderInitialized = true;
        }

        // Контейнер строго под размер потока: пересоздаём на первом кадре
        // и при любом изменении размера демонстрируемого окна
        if (decodeOutput == null || decodeOutput.Width != expectedWidth || decodeOutput.Height != expectedHeight)
        {
            decodeOutput = new RgbImage(ImageFormat.Rgba, expectedWidth, expectedHeight);
        }

        if (!decoder.Decode(encoded, 0, encoded.Length, noDelay: true, out _, ref decodeOutput))
        {
            return false;
        }

        var frameWidth = decodeOutput.Width;
        var frameHeight = decodeOutput.Height;

        if (frameWidth != expectedWidth || frameHeight != expectedHeight)
        {
            return false; // неожиданный размер — контейнер пересоздаётся на следующем кадре
        }

        var stride = decodeOutput.Stride;

        for (var y = 0; y < frameHeight; y++)
        {
            Marshal.Copy(IntPtr.Add(decodeOutput.NativeBytes, y * stride), targetBuffer, y * frameWidth * 4, frameWidth * 4);
        }

        return true;
    }

    void IDisposable.Dispose()
    {
        if (encoder is IDisposable disposableEncoder)
        {
            disposableEncoder.Dispose();
        }

        if (decoder is IDisposable disposableDecoder)
        {
            disposableDecoder.Dispose();
        }
    }

    // Сканирует Annex-B поток: NAL-юнит типа 5 (IDR slice) = ключевой кадр.
    // nal_unit_type — младшие 5 бит первого байта NAL-заголовка
    private static bool ContainsIdrNal(byte[] annexB)
    {
        var i = 0;

        while (i < annexB.Length - 4)
        {
            int startCodeLength;

            switch (annexB[i])
            {
                case 0 when annexB[i + 1] == 0 && annexB[i + 2] == 0 && annexB[i + 3] == 1:
                    startCodeLength = 4;
                    break;
                case 0 when annexB[i + 1] == 0 && annexB[i + 2] == 1:
                    startCodeLength = 3;
                    break;
                default:
                    i++;
                    continue;
            }

            var nalType = (byte)(annexB[i + startCodeLength] & 0x1F);

            if (nalType == 5)
            {
                return true;
            }

            i += startCodeLength + 1;
        }

        return false;
    }
}
