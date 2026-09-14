using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class VadMonitorDisposeTests
{
    private static string ResolveSileroPath()
    {
        var silero = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "models", "silero_vad.onnx"));
        if (!File.Exists(silero))
        {
            silero = Path.Combine(@"D:\AI_program\VoiceTyper\models", "silero_vad.onnx");
        }

        return silero;
    }

    [Fact]
    public void Push_after_Dispose_with_silero_does_not_throw()
    {
        var silero = ResolveSileroPath();
        Assert.True(File.Exists(silero), $"silero_vad.onnx missing at {silero}");

        using var vad = new VadMonitor(silero);
        Assert.True(vad.HasSilero);

        vad.Dispose();
        var boom = Record.Exception(() =>
        {
            for (var i = 0; i < 4; i++)
            {
                vad.Push(new float[VadMonitor.WindowSize]);
            }
        });

        Assert.Null(boom);
    }

    [Fact]
    public void Push_after_Dispose_energy_fallback_does_not_throw()
    {
        using var vad = new VadMonitor(sileroVadPath: null);
        vad.Dispose();
        var boom = Record.Exception(() => vad.Push(new float[320]));
        Assert.Null(boom);
    }

    [Fact]
    public async Task Concurrent_Push_during_Dispose_does_not_throw()
    {
        var silero = ResolveSileroPath();
        Assert.True(File.Exists(silero), $"silero_vad.onnx missing at {silero}");

        var vad = new VadMonitor(silero);
        Assert.True(vad.HasSilero);

        var boom = 0;
        using var cts = new CancellationTokenSource();
        var pusher = Task.Run(() =>
        {
            var rnd = new Random(7);
            while (!cts.IsCancellationRequested)
            {
                var buf = new float[VadMonitor.WindowSize];
                for (var i = 0; i < buf.Length; i++)
                {
                    buf[i] = (float)(rnd.NextDouble() * 0.05);
                }

                try
                {
                    vad.Push(buf);
                }
                catch
                {
                    Interlocked.Increment(ref boom);
                    break;
                }
            }
        });

        await Task.Delay(40);
        var disposeBoom = Record.Exception(() => vad.Dispose());
        await Task.Delay(80);
        cts.Cancel();
        await pusher.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Null(disposeBoom);
        Assert.Equal(0, boom);
    }

    [Fact]
    public void UtteranceEnded_handler_can_Dispose_without_deadlock()
    {
        using var vad = new VadMonitor(sileroVadPath: null);
        using var disposed = new ManualResetEventSlim(false);
        vad.UtteranceEnded += () =>
        {
            vad.Dispose();
            disposed.Set();
        };

        var speech = new float[320];
        Array.Fill(speech, 0.05f);
        var quiet = new float[320];
        var until = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < until && !disposed.IsSet)
        {
            var elapsed = 3 - (until - DateTime.UtcNow).TotalSeconds;
            vad.Push(elapsed < 1.1 ? speech : quiet);
        }

        Assert.True(disposed.Wait(TimeSpan.FromSeconds(2)), "Dispose from UtteranceEnded deadlocked (Fire still holds the VAD lock)");
    }
}
