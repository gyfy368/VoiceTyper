using System.IO;
using NAudio.Wave;
using VoiceTyper;
using VoiceTyper.Services;
using Xunit;
using Xunit.Abstractions;

namespace VoiceTyper.Tests;

/// <summary>
/// Reproduces the post-record stop path without the WPF window:
/// VAD (optional) → stop/collect audio → SenseVoice Transcribe.
/// A NullReferenceException here is the same failure the UI sanitizes
/// into 「出了点问题，请再点一次麦克风重试」.
/// </summary>
public class StopPathTests
{
    private readonly ITestOutputHelper _output;

    public StopPathTests(ITestOutputHelper output) => _output = output;

    private static bool TryModels(out ModelPaths paths)
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "models"));
        if (!Directory.Exists(root))
        {
            root = @"D:\AI_program\VoiceTyper\models";
        }

        return ModelLocator.TryResolve(root, out paths, out _);
    }

    private static float[] Tone(double seconds, double hz = 220)
    {
        var n = (int)(Recorder.SampleRate * seconds);
        var samples = new float[n];
        for (var i = 0; i < n; i++)
        {
            samples[i] = (float)(0.18 * Math.Sin(2 * Math.PI * hz * i / Recorder.SampleRate));
        }

        return samples;
    }

    [Fact]
    public void Transcribe_two_second_tone_does_not_throw_nullref()
    {
        Assert.True(TryModels(out var paths), "SenseVoice models missing");

        using var transcriber = new Transcriber();
        transcriber.EnsureLoaded(paths);
        Assert.True(transcriber.IsReady);

        var audio = Tone(2);
        Exception? boom = null;
        string? text = null;
        try
        {
            text = transcriber.Transcribe(audio);
        }
        catch (Exception ex)
        {
            boom = ex;
            _output.WriteLine($"{ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");
        }

        Assert.False(boom is NullReferenceException, boom?.ToString());
        Assert.Null(boom);
        Assert.NotNull(text);
    }

    [Fact]
    public void Transcribe_after_silero_vad_dispose_does_not_throw_nullref()
    {
        Assert.True(TryModels(out var paths), "SenseVoice models missing");
        Assert.False(string.IsNullOrWhiteSpace(paths.SileroVad));
        Assert.True(File.Exists(paths.SileroVad!));

        using var vad = new VadMonitor(paths.SileroVad);
        Assert.True(vad.HasSilero);
        vad.Push(Tone(1.2));
        vad.Dispose();

        using var transcriber = new Transcriber();
        transcriber.EnsureLoaded(paths);

        var boom = Record.Exception(() => transcriber.Transcribe(Tone(2)));
        if (boom is not null)
        {
            _output.WriteLine($"{boom.GetType().FullName}: {boom.Message}\n{boom.StackTrace}");
        }

        Assert.False(boom is NullReferenceException, boom?.ToString());
        Assert.Null(boom);
    }

    [Fact]
    public void Recorder_manual_stop_after_short_capture_does_not_throw_nullref()
    {
        if (WaveInEvent.DeviceCount <= 0)
        {
            _output.WriteLine("No capture device; skipping hardware stop path.");
            return;
        }

        using var recorder = new Recorder();
        recorder.Start();
        Thread.Sleep(900);
        var boom = Record.Exception(() => recorder.Stop());
        if (boom is not null)
        {
            _output.WriteLine($"{boom.GetType().FullName}: {boom.Message}\n{boom.StackTrace}");
        }

        Assert.False(boom is NullReferenceException, boom?.ToString());
        if (boom is InvalidOperationException && boom.Message.Contains("没录到声音"))
        {
            // Hardware produced too little audio; that is not the stop NRE.
            return;
        }

        Assert.Null(boom);
    }

    /// <summary>
    /// Production WaveInEvent captures WPF's DispatcherSynchronizationContext.
    /// xUnit has none, so the previous test missed that stop path.
    /// </summary>
    [Fact]
    public void Dispatcher_stop_with_live_vad_then_transcribe_does_not_throw_nullref()
    {
        if (WaveInEvent.DeviceCount <= 0)
        {
            _output.WriteLine("No capture device; skipping dispatcher stop path.");
            return;
        }

        Assert.True(TryModels(out var paths), "SenseVoice models missing");

        Exception? boom = null;
        string? transcript = null;
        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(
                    new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));

                var recorder = new Recorder();
                var transcriber = new Transcriber();
                transcriber.EnsureLoaded(paths);
                VadMonitor? vad = paths.SileroVad is null ? null : new VadMonitor(paths.SileroVad);
                if (vad is not null)
                {
                    recorder.SamplesAvailable += vad.Push;
                }

                recorder.Start();

                var frame = new System.Windows.Threading.DispatcherFrame();
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(1400)
                };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    try
                    {
                        if (vad is not null)
                        {
                            recorder.SamplesAvailable -= vad.Push;
                        }

                        var audio = recorder.Stop();
                        vad?.Dispose();
                        vad = null;
                        transcript = transcriber.Transcribe(audio);
                    }
                    catch (Exception ex)
                    {
                        boom = ex;
                    }
                    finally
                    {
                        try
                        {
                            vad?.Dispose();
                        }
                        catch
                        {
                            // ignore
                        }

                        recorder.Dispose();
                        transcriber.Dispose();
                        frame.Continue = false;
                    }
                };
                timer.Start();
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            }
            catch (Exception ex)
            {
                boom = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)), "dispatcher stop path hung");

        if (boom is not null)
        {
            _output.WriteLine($"{boom.GetType().FullName}: {boom.Message}\n{boom.StackTrace}");
        }

        Assert.False(boom is NullReferenceException, boom?.ToString());
        if (boom is InvalidOperationException && boom.Message.Contains("没录到声音"))
        {
            return;
        }

        Assert.Null(boom);
        Assert.NotNull(transcript);
    }

    [Fact]
    public void MainWindow_manual_mic_stop_does_not_nre()
    {
        if (WaveInEvent.DeviceCount <= 0)
        {
            _output.WriteLine("No capture device; skipping MainWindow stop path.");
            return;
        }

        Assert.True(TryModels(out _), "SenseVoice models missing");

        Exception? boom = null;
        string? stage = null;
        string? status = null;
        string? capsule = null;
        var thread = new Thread(() =>
        {
            CrashLog.Reset();
            CrashLog.SuppressModalDialogs = true;
            try
            {
                var app = new App();
                app.InitializeComponent();
                app.DispatcherUnhandledException += (_, e) =>
                {
                    CrashLog.Write("test-dispatcher-unhandled", e.Exception);
                    e.Handled = true;
                };

                var window = new MainWindow();
                window.Show();
                window.UpdateLayout();

                window.MicButton.RaiseEvent(new System.Windows.RoutedEventArgs(
                    System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

                var frame = new System.Windows.Threading.DispatcherFrame();
                var started = DateTime.UtcNow;
                var stopped = false;
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(200)
                };
                timer.Tick += (_, _) =>
                {
                    var elapsed = DateTime.UtcNow - started;
                    if (!stopped && elapsed.TotalMilliseconds >= 1500)
                    {
                        stopped = true;
                        window.MicButton.RaiseEvent(new System.Windows.RoutedEventArgs(
                            System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        return;
                    }

                    status = window.StatusText.Text;
                    capsule = window.CapsuleLabel.Text;
                    var done = capsule is "说一句" or "已复制" or "没听清" or "出错了"
                        || (status is not null && (
                            status.Contains("转写完成")
                            || status.Contains("已复制")
                            || status.Contains("没听清")
                            || status.Contains("出了点问题")
                            || status.Contains("没录到声音")));
                    if (CrashLog.LastException is not null || (stopped && done) || elapsed.TotalSeconds > 35)
                    {
                        timer.Stop();
                        boom = CrashLog.LastException;
                        stage = CrashLog.LastStage;
                        status = window.StatusText.Text;
                        capsule = window.CapsuleLabel.Text;
                        window.Close();
                        frame.Continue = false;
                    }
                };
                timer.Start();
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            }
            catch (Exception ex)
            {
                boom = ex;
                stage = "test-harness";
            }
            finally
            {
                CrashLog.SuppressModalDialogs = false;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(50)), "MainWindow stop path hung");

        _output.WriteLine($"stage={stage} capsule={capsule} status={status}");
        if (boom is not null)
        {
            _output.WriteLine($"{boom.GetType().FullName}: {boom.Message}\n{boom.StackTrace}");
        }

        Assert.False(boom is NullReferenceException, $"NRE at {stage}: {boom}");
        if (boom is InvalidOperationException && (boom.Message.Contains("没录到声音") || boom.Message.Contains("麦克风")))
        {
            return;
        }

        Assert.Null(boom);
    }

    [Fact]
    public void Dispatcher_stop_does_not_deadlock_waiting_for_wavein()
    {
        if (WaveInEvent.DeviceCount <= 0)
        {
            _output.WriteLine("No capture device; skipping deadlock test.");
            return;
        }

        Assert.True(TryModels(out var paths), "SenseVoice models missing");

        Exception? boom = null;
        var thread = new Thread(() =>
        {
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(
                new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
            Recorder? recorder = null;
            VadMonitor? vad = null;
            try
            {
                recorder = new Recorder();
                if (paths.SileroVad is not null)
                {
                    vad = new VadMonitor(paths.SileroVad);
                    recorder.SamplesAvailable += vad.Push;
                }

                recorder.Start();
                var frame = new System.Windows.Threading.DispatcherFrame();
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(700)
                };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    boom = Record.Exception(() => recorder.Stop());
                    frame.Continue = false;
                };
                timer.Start();
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            }
            catch (Exception ex)
            {
                boom = ex;
            }
            finally
            {
                try
                {
                    vad?.Dispose();
                }
                catch
                {
                    // ignore
                }

                recorder?.Dispose();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(
            thread.Join(TimeSpan.FromSeconds(8)),
            "Recorder.Stop deadlocked on the dispatcher (WaveInEvent captured WPF SynchronizationContext).");

        if (boom is not null)
        {
            _output.WriteLine($"{boom.GetType().FullName}: {boom.Message}\n{boom.StackTrace}");
        }

        Assert.False(boom is NullReferenceException, boom?.ToString());
        if (boom is InvalidOperationException && boom.Message.Contains("没录到声音"))
        {
            return;
        }

        Assert.Null(boom);
    }
}
