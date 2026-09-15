using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class TranscriptJoinTests
{
    [Fact]
    public void Replace_mode_discards_previous_text()
    {
        var text = TranscriptJoin.Apply("上一句", "新的一句", append: false);
        Assert.Equal("新的一句", text);
    }

    [Fact]
    public void Append_mode_keeps_previous_and_adds_incoming()
    {
        var text = TranscriptJoin.Apply("第一句", "第二句", append: true);
        Assert.Equal("第一句 第二句", text);
    }

    [Fact]
    public void Append_after_punctuation_does_not_add_extra_space()
    {
        Assert.Equal("你好。世界", TranscriptJoin.Apply("你好。", "世界", append: true));
        Assert.Equal("Hi. There", TranscriptJoin.Apply("Hi.", "There", append: true));
    }

    [Fact]
    public void Append_onto_empty_is_just_incoming()
    {
        Assert.Equal("开始", TranscriptJoin.Apply("  ", "开始", append: true));
        Assert.Equal("开始", TranscriptJoin.Apply(null, "开始", append: true));
    }

    [Fact]
    public void Incoming_is_trimmed()
    {
        Assert.Equal("打开终端", TranscriptJoin.Apply(null, "  打开终端  ", append: false));
        Assert.Equal("已有 打开终端", TranscriptJoin.Apply("已有", "  打开终端  ", append: true));
    }

    [Fact]
    public void Empty_incoming_keeps_existing_when_appending()
    {
        Assert.Equal("已有", TranscriptJoin.Apply("已有", "   ", append: true));
    }

    [Fact]
    public void Continue_inserts_at_caret_instead_of_the_end()
    {
        var join = TranscriptJoin.ApplyAt("前段后段", "插入", append: true, caretIndex: 2);
        Assert.Equal("前段 插入后段", join.Text);
        Assert.Equal("插入", join.Segment);
        Assert.Equal(3, join.InsertStart);
        Assert.Equal(2, join.InsertLength);
    }

    [Fact]
    public void Continue_at_end_matches_legacy_append()
    {
        var join = TranscriptJoin.ApplyAt("第一句", "第二句", append: true, caretIndex: 3);
        Assert.Equal("第一句 第二句", join.Text);
        Assert.Equal(4, join.InsertStart);
    }

    [Fact]
    public void Incoming_mixed_punctuation_is_cleaned_before_join()
    {
        Assert.Equal("你好。", TranscriptJoin.Apply(null, "你好。,", append: false));
    }
}
