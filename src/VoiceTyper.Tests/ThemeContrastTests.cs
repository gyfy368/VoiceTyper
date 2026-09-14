using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class ThemeContrastTests
{
    public static IEnumerable<object[]> AllThemes() =>
    [
        [ColorThemes.InkBlack],
        [ColorThemes.WarmPaper],
        [ColorThemes.CoolGray],
        [ColorThemes.Forest]
    ];

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void Body_text_contrasts_with_paper_and_well(string theme)
    {
        var p = ThemeService.GetPalette(theme);
        Assert.True(ThemeContrast.Ratio(p.Ivory, p.Paper) >= 4.5, $"{theme} Ivory on Paper");
        Assert.True(ThemeContrast.Ratio(p.Ivory, p.Well) >= 4.5, $"{theme} Ivory on Well");
        Assert.True(ThemeContrast.Ratio(p.Ivory, p.Plate) >= 4.5, $"{theme} Ivory on Plate");
    }

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void Mic_glyph_contrasts_with_halo(string theme)
    {
        var p = ThemeService.GetPalette(theme);
        Assert.True(ThemeContrast.Ratio(p.OnHalo, p.Halo) >= 4.5, $"{theme} OnHalo on Halo");
        if (ThemeContrast.IsDark(p.Paper))
        {
            Assert.True(ThemeContrast.Ratio(p.OnHalo, p.Paper) >= 4.5, $"{theme} glyph vs Paper");
        }
        else
        {
            Assert.True(ThemeContrast.Ratio(p.Halo, p.Paper) >= 3.0, $"{theme} Halo vs Paper");
            Assert.True(ThemeContrast.IsDark(p.Halo), $"{theme} halo should read as a dark seal");
            Assert.False(ThemeContrast.IsDark(p.OnHalo), $"{theme} glyph should be light on the seal");
        }
    }

    [Fact]
    public void Warm_paper_halo_is_not_ivory_on_ivory()
    {
        var p = ThemeService.GetPalette(ColorThemes.WarmPaper);
        Assert.NotEqual(p.Ivory, p.OnHalo);
        Assert.True(ThemeContrast.IsDark(p.Halo));
        Assert.False(ThemeContrast.IsDark(p.OnHalo));
    }

    [Fact]
    public void Collapse_hint_names_auto_copy_without_ruokaiqi()
    {
        Assert.DoesNotContain("若开启", UiCopy.CollapseAfterDoneHint);
        Assert.Contains("剪贴板", UiCopy.CollapseAfterDoneHint);
        Assert.Contains("先复制", UiCopy.CollapseAfterDoneHint);
    }
}
