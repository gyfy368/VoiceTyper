using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class StateMachineTests
{
    [Fact]
    public void Idle_to_done_happy_path()
    {
        var session = new SessionController();
        Assert.Equal(AppPhase.Idle, session.Phase);
        Assert.True(session.CanStart);
        Assert.False(session.CanStop);

        Assert.True(session.TryStartRecording());
        Assert.Equal(AppPhase.Recording, session.Phase);
        Assert.False(session.CanStart);
        Assert.True(session.CanStop);
        Assert.True(session.IsBusy);

        Assert.False(session.TryStartRecording());
        Assert.Equal(AppPhase.Recording, session.Phase);

        Assert.True(session.TryBeginTranscribing());
        Assert.Equal(AppPhase.Transcribing, session.Phase);
        Assert.False(session.CanStart);
        Assert.False(session.CanStop);
        Assert.False(session.TryStartRecording());
        Assert.False(session.TryBeginTranscribing());

        Assert.True(session.TryMarkDone());
        Assert.Equal(AppPhase.Done, session.Phase);
        Assert.True(session.CanStart);
        Assert.False(session.IsBusy);
    }

    [Fact]
    public void Transcribing_clicks_are_ignored()
    {
        var session = new SessionController();
        Assert.True(session.TryStartRecording());
        Assert.True(session.TryBeginTranscribing());
        Assert.False(session.TryStartRecording());
        Assert.False(session.TryBeginTranscribing());
        Assert.Equal(AppPhase.Transcribing, session.Phase);
    }

    [Fact]
    public void Cannot_transcribe_from_idle()
    {
        var session = new SessionController();
        Assert.False(session.TryBeginTranscribing());
        Assert.False(session.TryMarkDone());
        Assert.Equal(AppPhase.Idle, session.Phase);
    }

    [Fact]
    public void Done_can_start_again()
    {
        var session = new SessionController();
        session.TryStartRecording();
        session.TryBeginTranscribing();
        session.TryMarkDone();
        Assert.True(session.TryStartRecording());
        Assert.Equal(AppPhase.Recording, session.Phase);
    }

    [Fact]
    public void Failure_returns_to_idle()
    {
        var session = new SessionController();
        session.TryStartRecording();
        session.TryBeginTranscribing();
        session.ReturnToIdle();
        Assert.Equal(AppPhase.Idle, session.Phase);
        Assert.True(session.CanStart);
    }
}
