using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VoiceTyper.Services;

public sealed class Recorder : IDisposable
{
    public const int SampleRate = 16000;

    private WaveInEvent? _waveIn;
    private readonly List<float> _samples = new();
    private readonly object _gate = new();
    private DateTime _startedUtc;
    private volatile bool _capturing;

    public bool IsRecording => _capturing && _waveIn is not null;
    public TimeSpan Elapsed => IsRecording ? DateTime.UtcNow - _startedUtc : TimeSpan.Zero;

    public event Action<float[]>? SamplesAvailable;
    public event Action<float>? LevelChanged;

    public void Start()
    {
        if (_waveIn is not null)
        {
            throw new InvalidOperationException("已经在录音。");
        }

        if (WaveInEvent.DeviceCount <= 0)
        {
            throw new InvalidOperationException(
                "找不到麦克风。打开 Windows 设置 → 隐私和安全性 → 麦克风，允许桌面应用访问，并确认默认输入设备可用。");
        }

        lock (_gate)
        {
            _samples.Clear();
        }

        // WaveInEvent captures SynchronizationContext.Current. On the WPF UI thread that
        // marshals RecordingStopped back to the dispatcher. Stop() must wait for the
        // record thread to leave DataAvailable (Silero VAD runs there) before Dispose.
        // Waiting on the UI thread while that event is posted to the same dispatcher
        // deadlocks — so construct WaveInEvent with no sync context.
        var waveIn = CreateWaveIn();
        waveIn.DataAvailable += OnData;
        waveIn.RecordingStopped += OnStopped;

        try
        {
            waveIn.StartRecording();
        }
        catch (Exception ex)
        {
            waveIn.DataAvailable -= OnData;
            waveIn.RecordingStopped -= OnStopped;
            waveIn.Dispose();
            throw new InvalidOperationException(
                "麦克风被占用或没有权限。打开 Windows 设置 → 隐私和安全性 → 麦克风，允许桌面应用访问。\n" +
                ex.Message, ex);
        }

        _capturing = true;
        _waveIn = waveIn;
        _startedUtc = DateTime.UtcNow;
    }

    public float[] Stop()
    {
        _capturing = false;
        var waveIn = Interlocked.Exchange(ref _waveIn, null);
        if (waveIn is not null)
        {
            using var done = new ManualResetEventSlim(false);
            void OnStoppedOnce(object? sender, StoppedEventArgs e) => done.Set();
            waveIn.RecordingStopped += OnStoppedOnce;
            try
            {
                waveIn.StopRecording();
                // Record thread raises RecordingStopped after leaving DataAvailable.
                done.Wait(TimeSpan.FromSeconds(2));
            }
            finally
            {
                waveIn.RecordingStopped -= OnStoppedOnce;
                waveIn.DataAvailable -= OnData;
                waveIn.RecordingStopped -= OnStopped;
                try
                {
                    waveIn.Dispose();
                }
                catch
                {
                    // Driver teardown can still race; samples are already copied below.
                }
            }
        }

        float[] copy;
        lock (_gate)
        {
            copy = _samples.ToArray();
            _samples.Clear();
        }

        if (copy.Length < SampleRate / 5)
        {
            throw new InvalidOperationException("没录到声音，检查麦克风权限或默认设备。");
        }

        return copy;
    }

    public static string DescribeDevices()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            if (devices.Count == 0)
            {
                return "当前没有可用的录音设备。";
            }

            return "可用麦克风:\n" + string.Join("\n", devices.Select(d => "· " + d.FriendlyName));
        }
        catch
        {
            return $"NAudio 看到 {WaveInEvent.DeviceCount} 个输入设备。";
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (!_capturing || e.Buffer is null || e.BytesRecorded <= 0)
        {
            return;
        }

        var floats = ToFloat(e.Buffer, e.BytesRecorded);
        lock (_gate)
        {
            if (!_capturing)
            {
                return;
            }

            _samples.AddRange(floats);
        }

        float peak = 0;
        foreach (var s in floats)
        {
            var a = Math.Abs(s);
            if (a > peak)
            {
                peak = a;
            }
        }

        LevelChanged?.Invoke(peak);
        SamplesAvailable?.Invoke(floats);
    }

    private static void OnStopped(object? sender, StoppedEventArgs e)
    {
        // Errors surface on Stop(); keep the callback quiet so the UI thread stays in charge.
    }

    private static WaveInEvent CreateWaveIn()
    {
        var saved = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return new WaveInEvent
            {
                WaveFormat = new WaveFormat(SampleRate, 16, 1),
                BufferMilliseconds = 32
            };
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(saved);
        }
    }

    private static float[] ToFloat(byte[] buffer, int bytesRecorded)
    {
        var n = bytesRecorded / 2;
        var samples = new float[n];
        for (var i = 0; i < n; i++)
        {
            var s = BitConverter.ToInt16(buffer, i * 2);
            samples[i] = s / 32768f;
        }

        return samples;
    }

    public void Dispose()
    {
        _capturing = false;
        if (_waveIn is not null)
        {
            try
            {
                Stop();
            }
            catch
            {
                try
                {
                    _waveIn?.Dispose();
                }
                catch
                {
                    // shutting down
                }

                _waveIn = null;
            }
        }
    }
}
