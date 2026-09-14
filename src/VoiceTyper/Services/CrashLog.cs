using System.IO;

namespace VoiceTyper.Services;

/// <summary>
/// Records the raw CLR exception for stop/transcribe failures.
/// The UI still shows Chinese; this is how we find the real NullReference.
/// </summary>
public static class CrashLog
{
    public static string FilePath { get; } =
        Path.Combine(Path.GetTempPath(), "voicetyper-last-error.txt");

    public static string? LastStage { get; private set; }

    public static Exception? LastException { get; private set; }

    public static bool SuppressModalDialogs { get; set; }

    public static void Reset()
    {
        LastStage = null;
        LastException = null;
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch
        {
            // ignore
        }
    }

    public static void Write(string stage, Exception ex)
    {
        LastStage = stage;
        LastException = ex;
        try
        {
            File.WriteAllText(FilePath,
                stage + Environment.NewLine + ex.GetType().FullName + Environment.NewLine + ex);
        }
        catch
        {
            // ignore
        }
    }
}
