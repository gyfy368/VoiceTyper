using System.IO;

namespace VoiceTyper.Services;

public sealed record ModelPaths(string SenseVoiceOnnx, string Tokens, string? SileroVad);

public static class ModelLocator
{
    public const string SenseVoiceFolder = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17";
    public const string SenseVoiceOnnxName = "model.int8.onnx";
    public const string TokensName = "tokens.txt";
    public const string SileroVadName = "silero_vad.onnx";

    public static string ResolveModelsRoot()
    {
        foreach (var candidate in EnumerateModelRoots())
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "models");
    }

    public static bool TryResolve(out ModelPaths paths, out string error)
        => TryResolve(ResolveModelsRoot(), out paths, out error);

    public static bool TryResolve(string modelsRoot, out ModelPaths paths, out string error)
    {
        var onnx = FirstExisting(
            Path.Combine(modelsRoot, SenseVoiceFolder, SenseVoiceOnnxName),
            Path.Combine(modelsRoot, SenseVoiceOnnxName));
        var tokens = FirstExisting(
            Path.Combine(modelsRoot, SenseVoiceFolder, TokensName),
            Path.Combine(modelsRoot, TokensName));
        var vad = FirstExisting(
            Path.Combine(modelsRoot, SileroVadName),
            Path.Combine(modelsRoot, SenseVoiceFolder, SileroVadName));

        if (onnx is null || tokens is null)
        {
            paths = new ModelPaths("", "", vad);
            error =
                "还没下载转写模型（SenseVoice Small）。\n" +
                $"请放到:\n{Path.Combine(modelsRoot, SenseVoiceFolder)}\n" +
                "里面需要 model.int8.onnx 和 tokens.txt。\n\n" +
                "图书馆 WiFi 经常打不开 GitHub / Hugging Face，切手机热点下一次，之后完全离线。";
            return false;
        }

        paths = new ModelPaths(onnx, tokens, vad);
        error = string.Empty;
        return true;
    }

    public static IEnumerable<string> EnumerateModelRoots()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "models");
        yield return Path.Combine(Environment.CurrentDirectory, "models");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            yield return Path.Combine(dir.FullName, "models");
            dir = dir.Parent;
        }
    }

    private static string? FirstExisting(params string[] candidates)
    {
        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
