using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class ClipboardPolicyTests
{
    private sealed class FakeClipboard : IClipboardWriter
    {
        public string? Last { get; private set; }
        public int Calls { get; private set; }

        public void SetText(string text)
        {
            Calls++;
            Last = text;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void Empty_text_must_not_copy(string? text)
    {
        Assert.False(ClipboardPolicy.ShouldCopy(text));
        var fake = new FakeClipboard();
        var service = new ClipboardService(fake);
        var ok = service.TryCopy(text, out var message);
        Assert.False(ok);
        Assert.Equal(0, fake.Calls);
        Assert.Null(fake.Last);
        Assert.Contains("没听清", message);
    }

    [Fact]
    public void Non_empty_text_is_trimmed_and_copied()
    {
        var fake = new FakeClipboard();
        var service = new ClipboardService(fake);
        var ok = service.TryCopy("  打开终端  ", out var message);
        Assert.True(ok);
        Assert.Equal(1, fake.Calls);
        Assert.Equal("打开终端", fake.Last);
        Assert.Equal("已复制", message);
    }
}
