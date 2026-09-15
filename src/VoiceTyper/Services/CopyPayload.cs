namespace VoiceTyper.Services;

public static class CopyModes
{
    public const string Entire = "Entire";
    public const string NewOnly = "NewOnly";
}

public readonly record struct JoinResult(string Text, int InsertStart, int InsertLength, string Segment);

public static class CopyPayload
{
    public static string Resolve(string entireText, string incomingSegment, bool wasAppend, string? copyMode)
    {
        if (wasAppend &&
            string.Equals(copyMode, CopyModes.NewOnly, StringComparison.OrdinalIgnoreCase))
        {
            return incomingSegment ?? string.Empty;
        }

        return entireText ?? string.Empty;
    }
}
