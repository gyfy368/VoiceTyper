using SherpaOnnx;

namespace VoiceTyper.Services;

public sealed class Transcriber : IDisposable
{
    private readonly object _gate = new();
    private OfflineRecognizer? _recognizer;
    private ModelPaths? _paths;

    public bool IsReady
    {
        get
        {
            lock (_gate)
            {
                return _recognizer is not null;
            }
        }
    }

    public void EnsureLoaded(ModelPaths? paths = null)
    {
        lock (_gate)
        {
            if (_recognizer is not null)
            {
                return;
            }

            if (paths is null)
            {
                if (!ModelLocator.TryResolve(out var resolved, out var error))
                {
                    throw new InvalidOperationException(error);
                }

                paths = resolved;
            }

            var config = new OfflineRecognizerConfig();
            config.FeatConfig.SampleRate = Recorder.SampleRate;
            config.FeatConfig.FeatureDim = 80;
            config.DecodingMethod = "greedy_search";
            config.ModelConfig.Tokens = paths.Tokens;
            config.ModelConfig.Provider = "cpu";
            config.ModelConfig.NumThreads = Math.Clamp(Environment.ProcessorCount / 2, 2, 8);
            config.ModelConfig.Debug = 0;
            config.ModelConfig.SenseVoice.Model = paths.SenseVoiceOnnx;
            config.ModelConfig.SenseVoice.Language = "auto";
            config.ModelConfig.SenseVoice.UseInverseTextNormalization = 1;

            _recognizer = new OfflineRecognizer(config);
            _paths = paths;
        }
    }

    public string Transcribe(float[] samples, int sampleRate = Recorder.SampleRate)
    {
        if (samples.Length == 0)
        {
            return string.Empty;
        }

        EnsureLoaded();
        OfflineRecognizer recognizer;
        lock (_gate)
        {
            recognizer = _recognizer ?? throw new InvalidOperationException("转写引擎还没就绪。");
        }

        var stream = recognizer.CreateStream()
            ?? throw new InvalidOperationException("转写引擎创建失败，请重试。");
        if (stream.Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("转写引擎创建失败，请重试。");
        }

        try
        {
            stream.AcceptWaveform(sampleRate, samples);
            recognizer.Decode(stream);
            var result = stream.Result;
            return PunctuationCleanup.Clean(result?.Text);
        }
        catch (NullReferenceException)
        {
            throw new InvalidOperationException("转写引擎没有返回结果，请再说一次。");
        }
        finally
        {
            (stream as IDisposable)?.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _recognizer?.Dispose();
            _recognizer = null;
            _paths = null;
        }
    }
}
