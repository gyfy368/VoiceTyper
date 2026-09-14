using System.IO;
using SherpaOnnx;

namespace VoiceTyper.Services;

/// <summary>
/// Feeds 16 kHz mono to Silero VAD. Falls back to energy if the onnx is missing.
/// Fires once when speech has been heard and then a pause is detected.
/// </summary>
public sealed class VadMonitor : IDisposable
{
    public const int WindowSize = 512;
    private const float SpeechRms = 0.018f;
    private const float QuietRms = 0.007f;
    private const int QuietMsToStop = 1100;
    private const int MinMsBeforeStop = 900;

    private readonly object _gate = new();
    private VoiceActivityDetector? _vad;
    private readonly List<float> _pending = new();
    private bool _heardSpeech;
    private int _quietMs;
    private int _elapsedMs;
    private int _fired;
    private bool _disposed;

    public event Action? UtteranceEnded;

    public bool HasSilero
    {
        get
        {
            lock (_gate)
            {
                return _vad is not null && !_disposed;
            }
        }
    }

    public VadMonitor(string? sileroVadPath)
    {
        if (string.IsNullOrWhiteSpace(sileroVadPath) || !File.Exists(sileroVadPath))
        {
            return;
        }

        try
        {
            var config = new VadModelConfig();
            config.SampleRate = Recorder.SampleRate;
            config.NumThreads = 1;
            config.Provider = "cpu";
            config.Debug = 0;
            config.SileroVad.Model = sileroVadPath;
            config.SileroVad.Threshold = 0.5f;
            config.SileroVad.MinSilenceDuration = 1.0f;
            config.SileroVad.MinSpeechDuration = 0.25f;
            config.SileroVad.MaxSpeechDuration = 35f;
            config.SileroVad.WindowSize = WindowSize;
            _vad = new VoiceActivityDetector(config, 60);
        }
        catch
        {
            _vad = null;
        }
    }

    public void Push(float[] samples)
    {
        if (_disposed || _fired == 1 || samples.Length == 0)
        {
            return;
        }

        var shouldFire = false;
        lock (_gate)
        {
            if (_disposed || _fired == 1)
            {
                return;
            }

            _elapsedMs += samples.Length * 1000 / Recorder.SampleRate;
            if (_vad is not null)
            {
                shouldFire = PushSilero(samples);
            }
            else
            {
                shouldFire = PushEnergy(samples);
            }
        }

        if (shouldFire)
        {
            Fire();
        }
    }

    private bool PushSilero(float[] samples)
    {
        var vad = _vad;
        if (vad is null || _disposed)
        {
            return false;
        }

        _pending.AddRange(samples);
        while (_pending.Count >= WindowSize)
        {
            if (_disposed || _vad is null)
            {
                return false;
            }

            var chunk = _pending.GetRange(0, WindowSize).ToArray();
            _pending.RemoveRange(0, WindowSize);
            try
            {
                vad.AcceptWaveform(chunk);
                if (vad.IsSpeechDetected())
                {
                    _heardSpeech = true;
                }

                while (!vad.IsEmpty())
                {
                    _ = vad.Front();
                    vad.Pop();
                    if (_heardSpeech && _elapsedMs >= MinMsBeforeStop)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Native VAD may already be destroyed on another thread during stop.
                return false;
            }
        }

        return false;
    }

    private bool PushEnergy(float[] samples)
    {
        double sum = 0;
        foreach (var s in samples)
        {
            sum += s * s;
        }

        var rms = Math.Sqrt(sum / samples.Length);
        var ms = samples.Length * 1000 / Recorder.SampleRate;
        if (rms >= SpeechRms)
        {
            _heardSpeech = true;
            _quietMs = 0;
            return false;
        }

        if (!_heardSpeech)
        {
            return false;
        }

        if (rms < QuietRms)
        {
            _quietMs += ms;
            if (_quietMs >= QuietMsToStop && _elapsedMs >= MinMsBeforeStop)
            {
                return true;
            }
        }
        else
        {
            _quietMs = 0;
        }

        return false;
    }

    private void Fire()
    {
        if (Interlocked.Exchange(ref _fired, 1) == 0)
        {
            UtteranceEnded?.Invoke();
        }
    }

    public void Dispose()
    {
        VoiceActivityDetector? vad;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            vad = _vad;
            _vad = null;
            _pending.Clear();
        }

        try
        {
            vad?.Dispose();
        }
        catch
        {
            // Ignore double-free / native teardown races.
        }
    }
}
