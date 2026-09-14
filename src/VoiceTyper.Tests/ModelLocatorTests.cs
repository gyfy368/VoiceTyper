using System.IO;
using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class ModelLocatorTests
{
    [Fact]
    public void Missing_folder_returns_chinese_error()
    {
        var root = Path.Combine(Path.GetTempPath(), "voice-typer-missing-" + Guid.NewGuid().ToString("N"));
        var ok = ModelLocator.TryResolve(root, out var paths, out var error);
        Assert.False(ok);
        Assert.True(string.IsNullOrEmpty(paths.SenseVoiceOnnx));
        Assert.Contains("还没下载", error);
        Assert.Contains(root, error);
    }

    [Fact]
    public void Finds_onnx_and_tokens_in_sensevoice_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "voice-typer-models-" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root, ModelLocator.SenseVoiceFolder);
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, ModelLocator.SenseVoiceOnnxName), "fake");
            File.WriteAllText(Path.Combine(folder, ModelLocator.TokensName), "a");
            var ok = ModelLocator.TryResolve(root, out var paths, out var error);
            Assert.True(ok);
            Assert.Equal(string.Empty, error);
            Assert.True(File.Exists(paths.SenseVoiceOnnx));
            Assert.True(File.Exists(paths.Tokens));
            Assert.Null(paths.SileroVad);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
