using System.IO;
using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class PaperSizeTests
{
    [Fact]
    public void Defaults_include_medium_paper_size()
    {
        var s = new AppSettings();
        Assert.Equal(PaperSize.DefaultWidth, s.PaperWidth);
        Assert.Equal(PaperSize.DefaultHeight, s.PaperHeight);
        Assert.Equal(PaperSize.Presets.Medium, s.PaperSizePreset);
    }

    [Theory]
    [InlineData(100, 100, PaperSize.MinWidth, PaperSize.MinHeight)]
    [InlineData(9999, 9999, PaperSize.MaxWidth, PaperSize.MaxHeight)]
    [InlineData(320, 428, 320, 428)]
    public void Clamp_keeps_size_in_safe_range(double w, double h, double ew, double eh)
    {
        var (cw, ch) = PaperSize.Clamp(w, h);
        Assert.Equal(ew, cw);
        Assert.Equal(eh, ch);
    }

    [Theory]
    [InlineData(PaperSize.Presets.Small, 280, 380)]
    [InlineData(PaperSize.Presets.Medium, 320, 428)]
    [InlineData(PaperSize.Presets.Large, 400, 560)]
    public void Preset_resolves_to_expected_size(string preset, double w, double h)
    {
        var (pw, ph) = PaperSize.FromPreset(preset);
        Assert.Equal(w, pw);
        Assert.Equal(h, ph);
    }

    [Fact]
    public void Roundtrip_persists_paper_size()
    {
        var path = Path.Combine(Path.GetTempPath(), "voicetyper-paper-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var original = new AppSettings
            {
                PaperWidth = 400,
                PaperHeight = 560,
                PaperSizePreset = PaperSize.Presets.Large
            };
            SettingsStore.Save(original, path);
            var loaded = SettingsStore.Load(path);
            Assert.Equal(400, loaded.PaperWidth);
            Assert.Equal(560, loaded.PaperHeight);
            Assert.Equal(PaperSize.Presets.Large, loaded.PaperSizePreset);
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
    public void Normalize_clamps_out_of_range_paper_size()
    {
        var n = SettingsStore.Normalize(new AppSettings
        {
            PaperWidth = 10,
            PaperHeight = 9000,
            PaperSizePreset = "huge"
        });
        Assert.Equal(PaperSize.MinWidth, n.PaperWidth);
        Assert.Equal(PaperSize.MaxHeight, n.PaperHeight);
        Assert.Equal(PaperSize.Presets.Custom, n.PaperSizePreset);
    }
}
