namespace VoiceTyper.Services;

public interface IClipboardWriter
{
    void SetText(string text);
}

public sealed class WpfClipboardWriter : IClipboardWriter
{
    public void SetText(string text) => System.Windows.Clipboard.SetText(text);
}

public sealed class ClipboardService
{
    private readonly IClipboardWriter _writer;

    public ClipboardService(IClipboardWriter? writer = null)
    {
        _writer = writer ?? new WpfClipboardWriter();
    }

    /// <summary>
    /// Copies trimmed text. Empty / whitespace must not touch the clipboard.
    /// </summary>
    public bool TryCopy(string? text, out string message)
    {
        if (!ClipboardPolicy.ShouldCopy(text))
        {
            message = "没听清，重说一段。";
            return false;
        }

        _writer.SetText(text!.Trim());
        message = "已复制";
        return true;
    }
}
