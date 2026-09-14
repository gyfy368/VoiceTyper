namespace VoiceTyper.Services;

public enum MicSlot
{
    Single,
    NewSentence,
    Continue
}

public static class MicChrome
{
    public const string NewSentenceLabel = "新开一句";
    public const string ContinueLabel = "继续说";

    public static bool UseDualLayout(AppPhase phase, bool hasTranscript, MicSlot recordSlot)
    {
        if (phase is AppPhase.Recording or AppPhase.Transcribing)
        {
            return recordSlot is MicSlot.NewSentence or MicSlot.Continue;
        }

        return hasTranscript;
    }

    public static bool CanUseIdleActions(AppPhase phase, MicSlot _) =>
        phase is AppPhase.Idle or AppPhase.Done;

    public static bool AppendNext(MicSlot slot) => slot == MicSlot.Continue;
}
