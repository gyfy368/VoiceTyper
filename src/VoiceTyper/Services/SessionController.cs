namespace VoiceTyper.Services;

/// <summary>
/// Idle → Recording → Transcribing → Done → Idle.
/// Recording blocks a second start. Transcribing ignores idle clicks.
/// </summary>
public sealed class SessionController
{
    public AppPhase Phase { get; private set; } = AppPhase.Idle;

    public bool CanStart => Phase is AppPhase.Idle or AppPhase.Done;

    public bool CanStop => Phase == AppPhase.Recording;

    public bool IsBusy => Phase is AppPhase.Recording or AppPhase.Transcribing;

    public bool TryStartRecording()
    {
        if (!CanStart)
        {
            return false;
        }

        Phase = AppPhase.Recording;
        return true;
    }

    public bool TryBeginTranscribing()
    {
        if (Phase != AppPhase.Recording)
        {
            return false;
        }

        Phase = AppPhase.Transcribing;
        return true;
    }

    public bool TryMarkDone()
    {
        if (Phase != AppPhase.Transcribing)
        {
            return false;
        }

        Phase = AppPhase.Done;
        return true;
    }

    public void ReturnToIdle()
    {
        Phase = AppPhase.Idle;
    }
}
