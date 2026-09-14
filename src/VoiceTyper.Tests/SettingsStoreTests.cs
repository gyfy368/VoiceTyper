using System.IO;
using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void Defaults_match_delivery_product_expectations()
    {
        var s = new AppSettings();
        Assert.Equal(CloseBehaviors.Exit, s.CloseBehavior);
        Assert.True(s.ShowInTaskbar);
        Assert.True(s.Topmost);
        Assert.False(s.CollapseOnDeactivate);
        Assert.True(s.VadAutoStop);
        Assert.True(s.AutoCopy);
        Assert.False(s.CollapseAfterDone);
        Assert.Equal(DockSides.Right, s.DockSide);
        Assert.Equal(ShadowModes.Off, s.ShadowMode);
        Assert.True(s.AnimationsEnabled);
        Assert.Equal(ColorThemes.InkBlack, s.ColorTheme);
        Assert.Equal(PaperSize.DefaultWidth, s.PaperWidth);
        Assert.Equal(PaperSize.DefaultHeight, s.PaperHeight);
        Assert.Equal(PaperSize.Presets.Medium, s.PaperSizePreset);
        Assert.False(s.CloseToTray);
        Assert.True(s.DockRight);
        Assert.False(s.ShadowLight);
    }

    [Fact]
    public void Roundtrip_persists_custom_values()
    {
        var path = Path.Combine(Path.GetTempPath(), "voicetyper-settings-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var original = new AppSettings
            {
                CloseBehavior = CloseBehaviors.Tray,
                ShowInTaskbar = false,
                Topmost = false,
                CollapseOnDeactivate = true,
                VadAutoStop = false,
                AutoCopy = false,
                CollapseAfterDone = true,
                DockSide = DockSides.Left,
                ShadowMode = ShadowModes.Light,
                AnimationsEnabled = false,
                PaperLeft = 640,
                PaperTop = 200,
                ColorTheme = ColorThemes.Forest,
                PaperSizePreset = PaperSize.Presets.Large,
                PaperWidth = 400,
                PaperHeight = 560
            };
            SettingsStore.Save(original, path);
            var loaded = SettingsStore.Load(path);
            Assert.Equal(CloseBehaviors.Tray, loaded.CloseBehavior);
            Assert.False(loaded.ShowInTaskbar);
            Assert.False(loaded.Topmost);
            Assert.True(loaded.CollapseOnDeactivate);
            Assert.False(loaded.VadAutoStop);
            Assert.False(loaded.AutoCopy);
            Assert.True(loaded.CollapseAfterDone);
            Assert.Equal(DockSides.Left, loaded.DockSide);
            Assert.Equal(ShadowModes.Off, loaded.ShadowMode);
            Assert.True(loaded.AnimationsEnabled);
            Assert.False(loaded.ShadowLight);
            Assert.Equal(640, loaded.PaperLeft);
            Assert.Equal(200, loaded.PaperTop);
            Assert.Equal(ColorThemes.Forest, loaded.ColorTheme);
            Assert.Equal(PaperSize.Presets.Large, loaded.PaperSizePreset);
            Assert.Equal(400, loaded.PaperWidth);
            Assert.Equal(560, loaded.PaperHeight);
            Assert.True(loaded.CloseToTray);
            Assert.False(loaded.DockRight);
            Assert.False(loaded.ShadowLight);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Missing_file_returns_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "voicetyper-missing-" + Guid.NewGuid().ToString("N") + ".json");
        var loaded = SettingsStore.Load(path);
        Assert.Equal(CloseBehaviors.Exit, loaded.CloseBehavior);
        Assert.True(loaded.ShowInTaskbar);
        Assert.False(loaded.CollapseAfterDone);
        Assert.Equal(ColorThemes.InkBlack, loaded.ColorTheme);
    }

    [Fact]
    public void Normalize_rejects_unknown_enums()
    {
        var weird = new AppSettings
        {
            CloseBehavior = "explode",
            DockSide = "top",
            ShadowMode = "heavy",
            ColorTheme = "neon-purple"
        };
        var n = SettingsStore.Normalize(weird);
        Assert.Equal(CloseBehaviors.Exit, n.CloseBehavior);
        Assert.Equal(DockSides.Right, n.DockSide);
        Assert.Equal(ShadowModes.Off, n.ShadowMode);
        Assert.Equal(ColorThemes.InkBlack, n.ColorTheme);
    }

    [Fact]
    public void Normalize_forces_shadow_off_and_motion_on()
    {
        var n = SettingsStore.Normalize(new AppSettings
        {
            ShadowMode = ShadowModes.Light,
            AnimationsEnabled = false
        });
        Assert.Equal(ShadowModes.Off, n.ShadowMode);
        Assert.True(n.AnimationsEnabled);
        Assert.False(n.ShadowLight);
    }

    [Fact]
    public void Roundtrip_persists_paper_position_when_set()
    {
        var path = Path.Combine(Path.GetTempPath(), "voicetyper-pos-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            SettingsStore.Save(new AppSettings { PaperLeft = 412, PaperTop = 188 }, path);
            var loaded = SettingsStore.Load(path);
            Assert.Equal(412, loaded.PaperLeft);
            Assert.Equal(188, loaded.PaperTop);
            Assert.True(loaded.HasPaperPosition);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Missing_paper_position_stays_unset()
    {
        var s = new AppSettings();
        Assert.False(s.HasPaperPosition);
        var n = SettingsStore.Normalize(s);
        Assert.False(n.HasPaperPosition);
    }

    [Theory]
    [InlineData("WarmPaper", ColorThemes.WarmPaper)]
    [InlineData("coolgray", ColorThemes.CoolGray)]
    [InlineData("Forest", ColorThemes.Forest)]
    [InlineData("InkBlack", ColorThemes.InkBlack)]
    public void Normalize_accepts_known_color_themes(string input, string expected)
    {
        var n = SettingsStore.Normalize(new AppSettings { ColorTheme = input });
        Assert.Equal(expected, n.ColorTheme);
    }
}
