using System.IO;
using System.Net.Http;

namespace VoiceTyper.Services;

public sealed class DownloadProgress
{
    public required string FileName { get; init; }
    public long BytesReceived { get; init; }
    public long? TotalBytes { get; init; }
    public int Percent { get; init; }
}

public static class ModelDownloader
{
    public static readonly string[] SenseVoiceOnnxUrls =
    [
        "https://hf-mirror.com/csukuangfj/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17/resolve/main/model.int8.onnx",
        "https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17/resolve/main/model.int8.onnx"
    ];

    public static readonly string[] TokensUrls =
    [
        "https://hf-mirror.com/csukuangfj/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17/resolve/main/tokens.txt",
        "https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17/resolve/main/tokens.txt"
    ];

    public static readonly string[] SileroVadUrls =
    [
        "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/silero_vad.onnx",
        "https://hf-mirror.com/csukuangfj/silero_vad/resolve/main/silero_vad.onnx",
        "https://huggingface.co/csukuangfj/silero_vad/resolve/main/silero_vad.onnx"
    ];

    public const string ManualArchiveUrl =
        "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17.tar.bz2";

    public static async Task DownloadAsync(
        string modelsRoot,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.Combine(modelsRoot, ModelLocator.SenseVoiceFolder));
        Directory.CreateDirectory(modelsRoot);

        var onnx = Path.Combine(modelsRoot, ModelLocator.SenseVoiceFolder, ModelLocator.SenseVoiceOnnxName);
        var tokens = Path.Combine(modelsRoot, ModelLocator.SenseVoiceFolder, ModelLocator.TokensName);
        var vad = Path.Combine(modelsRoot, ModelLocator.SileroVadName);

        await DownloadFirstWorkingAsync(SenseVoiceOnnxUrls, onnx, progress, cancellationToken).ConfigureAwait(false);
        await DownloadFirstWorkingAsync(TokensUrls, tokens, progress, cancellationToken).ConfigureAwait(false);
        try
        {
            await DownloadFirstWorkingAsync(SileroVadUrls, vad, progress, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // VAD is optional; energy fallback still stops recording.
        }
    }

    private static async Task DownloadFirstWorkingAsync(
        IReadOnlyList<string> urls,
        string destPath,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (File.Exists(destPath) && new FileInfo(destPath).Length > 32)
        {
            return;
        }

        Exception? last = null;
        foreach (var url in urls)
        {
            try
            {
                await DownloadFileAsync(url, destPath, progress, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                last = ex;
            }
        }

        throw new InvalidOperationException(
            "模型下载失败。图书馆 WiFi 经常墙 GitHub / Hugging Face，请切手机热点后再试。\n" +
            $"手动下载: {ManualArchiveUrl}\n" +
            (last is null ? "" : last.Message));
    }

    private static async Task DownloadFileAsync(
        string url,
        string destPath,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var tmp = destPath + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(40) };
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[256 * 1024];
        long received = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;
            var percent = total is > 0 ? (int)(received * 100 / total.Value) : 0;
            progress?.Report(new DownloadProgress
            {
                FileName = Path.GetFileName(destPath),
                BytesReceived = received,
                TotalBytes = total,
                Percent = percent
            });
        }

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        output.Close();
        if (File.Exists(destPath))
        {
            File.Delete(destPath);
        }

        File.Move(tmp, destPath);
    }
}
