using System.Runtime.InteropServices;
using MIN.Audio.Services;
using MIN.Voice.Services.Contacts.Constants;
using MIN.Voice.Services.Contacts.Interfaces;
using OpenTK.Audio.OpenAL;

namespace MIN.Voice.Services.Models;

/// <summary>
/// Канал звука для одного участника
/// </summary>
internal sealed class ParticipantChannel : IDisposable
{
    private const int MaxBufferedFrames = 10;
    private const int NumBuffers = 4;

    private readonly IVoiceCodec codec;
    private readonly object gate = new();
    private readonly PlaybackDeviceContext deviceContext;
    private readonly int[] bufferIds = new int[NumBuffers];
    private readonly Queue<int> freeBuffers = new();
    private SortedDictionary<long, byte[]> pending = [];
    private int sourceId;
    private long? expectedNextSequence;
    private float currentGain;
    private bool disposed;

    /// <summary>
    /// Специфичный для участника громкость
    /// </summary>
    public float SpecificVolume;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ParticipantChannel"/>.
    /// Устройство/контекст - общие на всё приложение (<paramref name="deviceContext"/>),
    /// канал владеет только своим source и буферами.
    /// </summary>
    public ParticipantChannel(IVoiceCodec codec, PlaybackDeviceContext deviceContext, float startVolume, float specificVolume = 1.0f)
    {
        this.codec = codec;
        this.deviceContext = deviceContext;

        SpecificVolume = specificVolume;
        currentGain = Math.Clamp(startVolume * specificVolume, 0f, 1f);

        deviceContext.RunExclusive(CreateSourceAndBuffers);

        // Если общее устройство меняется, пересоздаём свой source/буферы
        // под новым контекстом.
        deviceContext.DeviceChanged += OnDeviceChanged;
    }

    // Вызывается ТОЛЬКО изнутри deviceContext.RunExclusive - контекст уже текущий.
    private void CreateSourceAndBuffers()
    {
        sourceId = AL.GenSource();
        AL.GenBuffers(NumBuffers, bufferIds);
        foreach (var buf in bufferIds)
        {
            freeBuffers.Enqueue(buf);
        }

        AL.Source(sourceId, ALSourcef.Gain, currentGain);
    }

    public void Enqueue(long sequenceNumber, byte[] data)
    {
        var decoded = codec.Decode(data);

        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            expectedNextSequence ??= sequenceNumber;

            if (sequenceNumber < expectedNextSequence.Value)
            {
                return;
            }

            pending[sequenceNumber] = decoded;

            if (pending.Count > MaxBufferedFrames)
            {
                var oldest = pending.Keys.First();
                expectedNextSequence = Math.Max(expectedNextSequence.Value, oldest + 1);
                pending.Remove(oldest);
            }

            // Всё общение с AL идёт под общим gate deviceContext - гарантирует,
            // что наш контекст точно текущий, даже если другой участник
            // (на другом потоке) тоже сейчас что-то делает с AL.
            deviceContext.RunExclusive(FlushPending);
        }
    }

    // Вызывается либо изнутри Enqueue (под gate И под RunExclusive), либо из
    // OnDeviceChanged (под RunExclusive). AL-контекст гарантированно текущий.
    private void FlushPending()
    {
        UpdateFreeBuffers();

        while (pending.Count > 0 && freeBuffers.Count > 0)
        {
            var seq = expectedNextSequence!.Value;
            if (!pending.TryGetValue(seq, out var samples))
            {
                break;
            }

            var buffer = freeBuffers.Dequeue();

            var handle = GCHandle.Alloc(samples, GCHandleType.Pinned);
            try
            {
                AL.BufferData(buffer, ALFormat.Mono16, handle.AddrOfPinnedObject(), samples.Length, VoiceAudioConstants.SampleRate);
            }
            finally
            {
                handle.Free();
            }

            AL.SourceQueueBuffer(sourceId, buffer);
            pending.Remove(seq);
            expectedNextSequence = seq + 1;

            AL.GetSource(sourceId, ALGetSourcei.SourceState, out var state);
            if ((ALSourceState)state != ALSourceState.Playing)
            {
                AL.SourcePlay(sourceId);
            }
        }
    }

    private void UpdateFreeBuffers()
    {
        AL.GetSource(sourceId, ALGetSourcei.BuffersProcessed, out var processed);
        if (processed <= 0)
        {
            return;
        }

        var processedBuffers = new int[processed];
        AL.SourceUnqueueBuffers(sourceId, processed, processedBuffers);
        foreach (var buf in processedBuffers)
        {
            freeBuffers.Enqueue(buf);
        }
    }

    public void ChangeVolume(float appVolume, float specificVolume)
    {
        SpecificVolume = specificVolume;
        var newGain = Math.Clamp(appVolume * specificVolume, 0f, 1f);

        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            deviceContext.RunExclusive(() => AL.Source(sourceId, ALSourcef.Gain, newGain));
            currentGain = newGain;
        }
    }

    // Устройство меняется централизованно через PlaybackDeviceContext.ChangeDevice -
    // этот канал просто пересоздаёт свой source/буферы под новым контекстом.
    // DeviceChanged поднимается ВНЕ lock самого deviceContext, так что тут
    // спокойно берём и свой gate, и RunExclusive.
    private void OnDeviceChanged()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            var pendingCopy = pending;
            var expectedSeq = expectedNextSequence;
            var gain = currentGain;

            freeBuffers.Clear();

            deviceContext.RunExclusive(CreateSourceAndBuffers);

            pending = pendingCopy;
            expectedNextSequence = expectedSeq;
            currentGain = gain;

            deviceContext.RunExclusive(FlushPending);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            deviceContext.DeviceChanged -= OnDeviceChanged;

            deviceContext.RunExclusive(() =>
            {
                AL.SourceStop(sourceId);
                AL.GetSource(sourceId, ALGetSourcei.BuffersQueued, out var queued);
                if (queued > 0)
                {
                    var queuedBuffers = new int[queued];
                    AL.SourceUnqueueBuffers(sourceId, queued, queuedBuffers);
                }
                AL.DeleteSource(sourceId);
                foreach (var buf in bufferIds)
                {
                    AL.DeleteBuffer(buf);
                }
            });

            disposed = true;
        }
    }
}
