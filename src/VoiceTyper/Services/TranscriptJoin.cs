namespace VoiceTyper.Services;

public static class TranscriptJoin
{
    public static string Apply(string? existing, string? incoming, bool append, int caretIndex = -1) =>
        ApplyAt(existing, incoming, append, caretIndex).Text;

    public static JoinResult ApplyAt(string? existing, string? incoming, bool append, int caretIndex = -1)
    {
        var next = PunctuationCleanup.Clean(incoming);
        if (!append)
        {
            return new JoinResult(next, 0, next.Length, next);
        }

        var prev = existing ?? string.Empty;
        if (string.IsNullOrWhiteSpace(prev))
        {
            return new JoinResult(next, 0, next.Length, next);
        }

        if (next.Length == 0)
        {
            var kept = prev.TrimEnd();
            return new JoinResult(kept, kept.Length, 0, string.Empty);
        }

        var index = caretIndex < 0 ? prev.Length : Math.Clamp(caretIndex, 0, prev.Length);
        if (index == prev.Length)
        {
            prev = prev.TrimEnd();
            index = prev.Length;
        }

        var left = prev[..index];
        var right = prev[index..];
        var pad = NeedsSeparator(left) && !StartsWithPunctuation(next) ? " " : string.Empty;
        var text = left + pad + next + right;
        return new JoinResult(text, left.Length + pad.Length, next.Length, next);
    }

    private static bool StartsWithPunctuation(string text) =>
        text.Length > 0 && "。．.，,、！!？?；;：:".Contains(text[0]);

    private static bool NeedsSeparator(string existing)
    {
        if (existing.Length == 0)
        {
            return false;
        }

        var c = existing[^1];
        if (char.IsWhiteSpace(c))
        {
            return false;
        }

        if ("。！？；：、…".Contains(c))
        {
            return false;
        }

        return true;
    }
}
