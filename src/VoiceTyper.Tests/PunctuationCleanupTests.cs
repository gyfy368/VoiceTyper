using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class PunctuationCleanupTests
{
    [Theory]
    [InlineData("你好。,", "你好。")]
    [InlineData("你好。，", "你好。")]
    [InlineData("你好,。", "你好。")]
    [InlineData("你好.,", "你好.")]
    [InlineData("你好。.", "你好。")]
    [InlineData("结束。,。", "结束。")]
    public void Mixed_trailing_period_comma_collapses_to_one_stop(string input, string expected)
    {
        Assert.Equal(expected, PunctuationCleanup.Clean(input));
    }

    [Fact]
    public void English_comma_inside_a_clause_is_kept()
    {
        Assert.Equal("Wait, what", PunctuationCleanup.Clean("Wait, what"));
        Assert.Equal("OK.", PunctuationCleanup.Clean("OK."));
    }

    [Fact]
    public void Clean_is_idempotent()
    {
        var once = PunctuationCleanup.Clean("录音结束。,");
        Assert.Equal(once, PunctuationCleanup.Clean(once));
    }

    [Fact]
    public void Whitespace_only_becomes_empty()
    {
        Assert.Equal(string.Empty, PunctuationCleanup.Clean("   "));
        Assert.Equal(string.Empty, PunctuationCleanup.Clean(null));
    }
}
