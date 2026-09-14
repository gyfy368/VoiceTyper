using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class ThemeServiceTests
{
    [Fact]
    public void GetPalette_ink_black_matches_legacy_paper_tone()
    {
        var p = ThemeService.GetPalette(ColorThemes.InkBlack);
        Assert.Equal(0x14, p.Ink.R);
        Assert.Equal(0x22, p.Paper.R);
        Assert.Equal(0xC8, p.Brass.R);
    }

    [Theory]
    [InlineData(ColorThemes.WarmPaper)]
    [InlineData(ColorThemes.CoolGray)]
    [InlineData(ColorThemes.Forest)]
    public void GetPalette_returns_distinct_paper_for_named_themes(string theme)
    {
        var ink = ThemeService.GetPalette(ColorThemes.InkBlack);
        var other = ThemeService.GetPalette(theme);
        Assert.NotEqual(ink.Paper, other.Paper);
    }

    [Fact]
    public void GetPalette_unknown_falls_back_to_ink_black()
    {
        var a = ThemeService.GetPalette("nope");
        var b = ThemeService.GetPalette(ColorThemes.InkBlack);
        Assert.Equal(b.Paper, a.Paper);
        Assert.Equal(b.Brass, a.Brass);
    }
}
