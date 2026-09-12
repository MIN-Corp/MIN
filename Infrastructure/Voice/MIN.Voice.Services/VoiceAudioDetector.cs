using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Interfaces.SettingsServices;
using MIN.Helpers.Contracts.Models;
using MIN.Helpers.Contracts.Models.Enums;
using MIN.Voice.Services.Contacts.Constants;
using MIN.Voice.Services.Contacts.Interfaces;

namespace MIN.Voice.Services;

/// <inheritdoc cref="IVoiceAudioDetector"/>
public class VoiceAudioDetector : IVoiceAudioDetector, IDisposable
{
    private const int WindowSamples = 512;   // model's fixed input window @16kHz
    private const int ContextSamples = 64;   // carried from previous window
    private const int AdaptiveMarginDb = 10;
    private const float SpeechProbThreshold = 0.5f;

    private readonly ISettingsProvider settingsProvider;
    private readonly TimeSpan holdTimeMs = TimeSpan.FromMilliseconds(200);
    private readonly Queue<float> recentRms = new(100);
    private readonly float[] state = new float[256]; // combined h/c state, reset on new stream

    private DateTime lastSpeechTime = DateTime.MinValue;

    private float noiseFloor = -60f;
    private bool noiseFloorInitialized;
    private readonly object @lock = new();

    private int sensitivityDb;

    private readonly Task<InferenceSession?> sessionTask;
    private readonly float[] context = new float[ContextSamples];
    private bool propertyChangedSubscribed;
    private bool onnxEnabled;

    // Accumulates incoming 320-sample frames until we have enough for one 512-sample window
    private readonly List<float> pending = new(WindowSamples);

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="VoiceAudioDetector"/>
    /// </summary>
    public VoiceAudioDetector(ISettingsProvider settingsProvider, ILoggerProvider logger)
    {
        this.settingsProvider = settingsProvider;
        this.settingsProvider.OnSettingsSaved += OnSettingsChanged;

        var options = new SessionOptions
        {
            InterOpNumThreads = 1,
            IntraOpNumThreads = 1, // Silero is tiny; single-threaded avoids overhead/contention
        };
        sessionTask = Task.Run(() => LoadEmbeddedSession(options, logger));

        LoadSettings();
    }

    private static InferenceSession? LoadEmbeddedSession(SessionOptions options, ILoggerProvider logger)
    {
        try
        {
            var assembly = typeof(VoiceAudioDetector).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .Single(n => n.EndsWith("silero_vad.onnx"));

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);

            return new InferenceSession(ms.ToArray(), options);
        }
        catch (Exception ex)
        {
            logger.Log($"Не удалось загрузить библиотеку шумоподавления Onnx: {ex.Message}", LogLevel.Error);
        }

        return null;
    }

    private void LoadSettings()
    {
        var settings = settingsProvider.GetSettings();
        if (!propertyChangedSubscribed)
        {
            settings.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Settings.InputDeviceSensitivity))
                {
                    sensitivityDb = settings.InputDeviceSensitivity;
                }
            };
            propertyChangedSubscribed = true;
        }

        onnxEnabled = settings.NoiseReduction == NoiseReduction.Onnx;
        sensitivityDb = settings.InputDeviceSensitivity;
    }

    private void OnSettingsChanged()
    {
        LoadSettings();
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (@lock)
        {
            lastSpeechTime = DateTime.MinValue;
            recentRms.Clear();
            noiseFloor = -60f;
            noiseFloorInitialized = false;

            Array.Clear(state, 0, state.Length);
            Array.Clear(context, 0, context.Length);
            pending.Clear();
        }
    }

    bool IVoiceAudioDetector.IsVoice(byte[] pcmData)
    {
        var shortSamples = ToShortSamples(pcmData);
        var rmsDb = ComputeRmsDb(shortSamples);

        // Cheap reject: well below noise floor, don't even bother with the expensive features
        if (rmsDb < GetCurrentThreshold() - 6f)
        {
            UpdateNoiseFloor(rmsDb);
            return HoldGate(false);
        }

        var isSpeech = rmsDb > GetCurrentThreshold();

        if (onnxEnabled && sessionTask.IsCompletedSuccessfully)
        {
            var maxProb = 0f;
            foreach (var prob in ProcessFrame(shortSamples))
            {
                maxProb = Math.Max(maxProb, prob);
            }

            isSpeech = maxProb > SpeechProbThreshold;
        }

        if (!isSpeech)
        {
            UpdateNoiseFloor(rmsDb);
        }

        return HoldGate(isSpeech);
    }

    private static short[] ToShortSamples(byte[] pcmData)
    {
        if (pcmData.Length % 2 != 0)
        {
            throw new ArgumentException("PCM data must have even length (16-bit samples)");
        }

        var samples = new short[pcmData.Length / 2];
        Buffer.BlockCopy(pcmData, 0, samples, 0, pcmData.Length);
        return samples;
    }

    private bool HoldGate(bool isSpeech)
    {
        if (isSpeech)
        {
            lastSpeechTime = DateTime.UtcNow;
            return true;
        }
        return (DateTime.UtcNow - lastSpeechTime) < holdTimeMs;
    }

    /// <summary>
    /// Feed one captured frame (any length). Returns speech probabilities for each
    /// complete 512-sample window that could be formed — usually 0 or 1 per call
    /// given your 320-sample frames, since windows don't align 1:1 with frames.
    /// </summary>
    private List<float> ProcessFrame(ReadOnlySpan<short> pcmSamples)
    {
        foreach (var s in pcmSamples)
        {
            pending.Add(s / 32768f);
        }

        var probabilities = new List<float>();
        while (pending.Count >= WindowSamples)
        {
            var window = pending.GetRange(0, WindowSamples);
            pending.RemoveRange(0, WindowSamples);
            probabilities.Add(RunInference(window.ToArray()));
        }

        return probabilities;
    }

    private float RunInference(float[] windowSamples)
    {
        var effectiveInput = new float[ContextSamples + WindowSamples];
        context.CopyTo(effectiveInput, 0);
        windowSamples.CopyTo(effectiveInput, ContextSamples);

        Array.Copy(windowSamples, WindowSamples - ContextSamples, context, 0, ContextSamples);

        var inputTensor = new DenseTensor<float>(effectiveInput, [1, effectiveInput.Length]);
        var stateTensor = new DenseTensor<float>(state, [2, 1, 128]);
        var srTensor = new DenseTensor<long>(new long[] { VoiceAudioConstants.SampleRate }, [1]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor),
            NamedOnnxValue.CreateFromTensor("state", stateTensor),
            NamedOnnxValue.CreateFromTensor("sr", srTensor),
        };

        using var results = sessionTask.Result!.Run(inputs);

        var prob = results.First(r => r.Name == "output").AsTensor<float>().First();
        var newState = results.First(r => r.Name == "stateN").AsTensor<float>().ToArray();
        newState.CopyTo(state, 0);

        return prob;
    }

    private static float ComputeRmsDb(short[] samples)
    {
        long sum = 0;
        foreach (var s in samples)
        {
            sum += s * s;
        }

        var rms = Math.Sqrt((double)sum / samples.Length);
        return rms < 1e-10 ? -100f : (float)(20 * Math.Log10(rms / 32768.0));
    }

    private float GetCurrentThreshold()
        => noiseFloorInitialized ? noiseFloor + AdaptiveMarginDb : sensitivityDb;

    private void UpdateNoiseFloor(float rmsDb)
    {
        lock (@lock)
        {
            recentRms.Enqueue(rmsDb);
            if (recentRms.Count > 100)
            {
                recentRms.Dequeue();
            }

            if (recentRms.Count >= 30 && recentRms.All(v => v < -50f))
            {
                noiseFloor = recentRms.Average();
                noiseFloorInitialized = true;
            }
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        if (sessionTask is { IsCompletedSuccessfully: true })
        {
            sessionTask.Result?.Dispose();
        }
        settingsProvider.OnSettingsSaved -= OnSettingsChanged;
        Reset();
    }
}
