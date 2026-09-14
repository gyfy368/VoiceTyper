namespace VoiceTyper.Services;

public static class ClipboardPolicy
{
    public static bool ShouldCopy(string? text) => !string.IsNullOrWhiteSpace(text);
}
