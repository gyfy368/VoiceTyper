using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class MicChromeTests
{
    [Fact]
    public void Idle_empty_transcript_uses_single_mic()
    {
        Assert.False(MicChrome.UseDualLayout(AppPhase.Idle, hasTranscript: false, MicSlot.Single));
        Assert.False(MicChrome.UseDualLayout(AppPhase.Done, hasTranscript: false, MicSlot.Single));
    }

    [Fact]
    public void Idle_with_transcript_uses_dual_actions()
    {
        Assert.True(MicChrome.UseDualLayout(AppPhase.Idle, hasTranscript: true, MicSlot.Single));
        Assert.True(MicChrome.UseDualLayout(AppPhase.Done, hasTranscript: true, MicSlot.Single));
    }

    [Fact]
    public void Recording_from_single_stays_single()
    {
        Assert.False(MicChrome.UseDualLayout(AppPhase.Recording, hasTranscript: false, MicSlot.Single));
        Assert.False(MicChrome.UseDualLayout(AppPhase.Transcribing, hasTranscript: false, MicSlot.Single));
    }

    [Fact]
    public void Recording_from_dual_slot_stays_dual_even_if_text_cleared()
    {
        Assert.True(MicChrome.UseDualLayout(AppPhase.Recording, hasTranscript: false, MicSlot.NewSentence));
        Assert.True(MicChrome.UseDualLayout(AppPhase.Transcribing, hasTranscript: true, MicSlot.Continue));
    }

    [Fact]
    public void Labels_are_the_product_copy()
    {
        Assert.Equal("新开一句", MicChrome.NewSentenceLabel);
        Assert.Equal("继续说", MicChrome.ContinueLabel);
    }

    [Theory]
    [InlineData(AppPhase.Transcribing, MicSlot.Single, false)]
    [InlineData(AppPhase.Transcribing, MicSlot.Continue, false)]
    [InlineData(AppPhase.Recording, MicSlot.NewSentence, false)]
    [InlineData(AppPhase.Idle, MicSlot.Single, true)]
    [InlineData(AppPhase.Done, MicSlot.Single, true)]
    public void Inactive_dual_button_is_disabled_while_busy(AppPhase phase, MicSlot slot, bool expectEnabled)
    {
        Assert.Equal(expectEnabled, MicChrome.CanUseIdleActions(phase, slot));
    }

    [Fact]
    public void New_sentence_clears_then_replaces_next_transcript()
    {
        Assert.False(MicChrome.AppendNext(MicSlot.NewSentence));
        Assert.False(MicChrome.AppendNext(MicSlot.Single));
        Assert.True(MicChrome.AppendNext(MicSlot.Continue));
    }
}
