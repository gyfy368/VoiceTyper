namespace VoiceTyper.Services;

public static class TranscriptJoin
{
    public static string Apply(string? existing, string? incoming, bool append)
    {
        var next = (incoming ?? string.Empty).Trim();
        if (!append)
        {
            return next;
        }

        var prev = (existing ?? string.Empty).TrimEnd();
        if (string.IsNullOrWhiteSpace(prev))
        {
            return next;
        }

        if (next.Length == 0)
        {
            return prev.Trim();
        }

        return NeedsSeparator(prev) ? prev + " " + next : prev + next;
    }

    private static bool NeedsSeparator(string existing)
    {
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
