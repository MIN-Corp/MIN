using MIN.ScreenDemo.Services.Contracts.Constants;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Capture.Windows.Internal;

/// <summary>
/// Общий конвейер кадров звука: накопление s16le стерео 48 кГц, нарезка
/// на кадры по 20 мс, фильтр тишины (RMS). Используется обоими движками
/// захвата — process loopback (s16 напрямую) и device loopback (float → s16)
/// </summary>
internal sealed class AudioFrameComposer
{
    private readonly List<byte> pendingS16 = new(ScreenDemoConstants.AudioFrameBytes * 2);

    /// <summary>
    /// Добавить готовые s16le стерео-байты и опубликовать накопившиеся полные кадры
    /// </summary>
    public void AddS16(byte[] data, long timestampMs, Action<ScreenDemoAudioFrame> emit)
    {
        pendingS16.AddRange(data);
        EmitReady(timestampMs, emit);
    }

    /// <summary>
    /// Конвертировать interleaved float-сэмплы (все каналы источника) в s16le стерео
    /// и опубликовать накопившиеся полные кадры
    /// </summary>
    public void AddFloats(float[] source, int sourceFloatCount, int sourceChannels, long timestampMs, Action<ScreenDemoAudioFrame> emit)
    {
        var frames = sourceFloatCount / sourceChannels;
        var chunk = new byte[frames * ScreenDemoConstants.AudioChannels * 2];
        var outSample = 0;

        for (var frame = 0; frame < frames; frame++)
        {
            var baseIndex = frame * sourceChannels;
            var left = source[baseIndex];
            var right = sourceChannels == 1 ? left : source[baseIndex + 1];

            WriteSample(chunk, ref outSample, left);
            WriteSample(chunk, ref outSample, right);
        }

        pendingS16.AddRange(chunk);
        EmitReady(timestampMs, emit);
    }

    private static void WriteSample(byte[] target, ref int sampleIndex, float value)
    {
        var s = (short)Math.Clamp(value * short.MaxValue, short.MinValue, short.MaxValue);
        target[sampleIndex * 2] = (byte)(s & 0xFF);
        target[sampleIndex * 2 + 1] = (byte)((s >> 8) & 0xFF);
        sampleIndex++;
    }

    /// <summary>
    /// Публикует все накопленные полные 20-мс кадры (кадры тишины пропускаются).
    /// Событие поднимается на потоке диспетчеризации; подписчик обязан быть
    /// неблокирующим (транмиттер только кладёт кадр в свою очередь)
    /// </summary>
    private void EmitReady(long timestampMs, Action<ScreenDemoAudioFrame> emit)
    {
        while (pendingS16.Count >= ScreenDemoConstants.AudioFrameBytes)
        {
            if (!IsVoiced(0, ScreenDemoConstants.AudioFrameBytes))
            {
                pendingS16.RemoveRange(0, ScreenDemoConstants.AudioFrameBytes);
                continue;
            }

            var data = new byte[ScreenDemoConstants.AudioFrameBytes];
            pendingS16.CopyTo(0, data, 0, ScreenDemoConstants.AudioFrameBytes);
            pendingS16.RemoveRange(0, ScreenDemoConstants.AudioFrameBytes);

            emit(new ScreenDemoAudioFrame(data, timestampMs));
        }
    }

    /// <summary>
    /// Проверка «кадр не тишина» без аллокаций: RMS по s16-сэмплам среза
    /// </summary>
    private bool IsVoiced(int offset, int count)
    {
        double sumSquares = 0;
        var sampleCount = count / 2;

        for (var i = 0; i < sampleCount; i++)
        {
            var index = offset + i * 2;
            var s = (short)(pendingS16[index] | (pendingS16[index + 1] << 8));
            sumSquares += (double)s * s;
        }

        var rms = Math.Sqrt(sumSquares / sampleCount);
        return rms > ScreenDemoConstants.AudioSilenceRms;
    }
}
