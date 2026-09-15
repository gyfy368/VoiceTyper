using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class CopyPayloadTests
{
    [Fact]
    public void New_only_continue_copies_the_incoming_segment()
    {
        var copied = CopyPayload.Resolve("前文 新说的", "新说的", wasAppend: true, CopyModes.NewOnly);
        Assert.Equal("新说的", copied);
    }

    [Fact]
    public void Entire_mode_copies_the_full_transcript()
    {
        var copied = CopyPayload.Resolve("前文 新说的", "新说的", wasAppend: true, CopyModes.Entire);
        Assert.Equal("前文 新说的", copied);
    }

    [Fact]
    public void Replace_always_copies_the_full_new_sentence()
    {
        Assert.Equal("新的一句", CopyPayload.Resolve("新的一句", "新的一句", wasAppend: false, CopyModes.NewOnly));
        Assert.Equal("新的一句", CopyPayload.Resolve("新的一句", "新的一句", wasAppend: false, CopyModes.Entire));
    }
}
