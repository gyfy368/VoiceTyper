using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class ExceptionTextTests
{
    [Fact]
    public void NullReference_becomes_chinese_user_message()
    {
        var text = ExceptionText.ForUser(new NullReferenceException());
        Assert.DoesNotContain("Object reference", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("出了点问题", text);
    }

    [Fact]
    public void NullReference_message_english_is_rewritten()
    {
        var text = ExceptionText.ForUser(
            new NullReferenceException("Object reference not set to an instance of an object."));
        Assert.DoesNotContain("Object reference", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("出了点问题", text);
    }

    [Fact]
    public void Ordinary_chinese_message_is_kept()
    {
        var text = ExceptionText.ForUser(new InvalidOperationException("没录到声音，检查麦克风权限或默认设备。"));
        Assert.Equal("没录到声音，检查麦克风权限或默认设备。", text);
    }
}
