using System.Windows;
using System.Windows.Media;

namespace VoiceTyper.Services;

public readonly record struct ThemePalette(
    Color Ink,
    Color Paper,
    Color Ivory,
    Color Muted,
    Color Brass,
    Color Ember,
    Color Rule,
    Color Plate,
    Color Well,
    Color Placeholder,
    Color Halo,
    Color OnHalo);

public static class ThemeService
{
    public static ThemePalette GetPalette(string? themeId)
    {
        if (string.Equals(themeId, ColorThemes.WarmPaper, StringComparison.OrdinalIgnoreCase))
        {
            return new ThemePalette(
                Ink: Color.FromRgb(0x2C, 0x24, 0x1B),
                Paper: Color.FromRgb(0xE8, 0xDF, 0xD0),
                Ivory: Color.FromRgb(0x2A, 0x22, 0x18),
                Muted: Color.FromRgb(0x7A, 0x6A, 0x56),
                Brass: Color.FromRgb(0x9A, 0x6B, 0x3A),
                Ember: Color.FromRgb(0xB8, 0x4A, 0x32),
                Rule: Color.FromRgb(0xC9, 0xB8, 0x9E),
                Plate: Color.FromRgb(0xF3, 0xEC, 0xDF),
                Well: Color.FromRgb(0xF7, 0xF1, 0xE6),
                Placeholder: Color.FromArgb(0x88, 0x7A, 0x6A, 0x56),
                Halo: Color.FromRgb(0x3A, 0x28, 0x1C),
                OnHalo: Color.FromRgb(0xF6, 0xEE, 0xE2));
        }

        if (string.Equals(themeId, ColorThemes.CoolGray, StringComparison.OrdinalIgnoreCase))
        {
            return new ThemePalette(
                Ink: Color.FromRgb(0x14, 0x16, 0x18),
                Paper: Color.FromRgb(0x22, 0x25, 0x28),
                Ivory: Color.FromRgb(0xE8, 0xEB, 0xEE),
                Muted: Color.FromRgb(0x9A, 0xA3, 0xAB),
                Brass: Color.FromRgb(0x8F, 0xA4, 0xB8),
                Ember: Color.FromRgb(0xC4, 0x6B, 0x5A),
                Rule: Color.FromRgb(0x3A, 0x40, 0x46),
                Plate: Color.FromRgb(0x2C, 0x31, 0x36),
                Well: Color.FromRgb(0x1A, 0x1D, 0x20),
                Placeholder: Color.FromArgb(0x88, 0x9A, 0xA3, 0xAB),
                Halo: Color.FromRgb(0x3E, 0x48, 0x50),
                OnHalo: Color.FromRgb(0xEE, 0xF2, 0xF5));
        }

        if (string.Equals(themeId, ColorThemes.Forest, StringComparison.OrdinalIgnoreCase))
        {
            return new ThemePalette(
                Ink: Color.FromRgb(0x0F, 0x16, 0x12),
                Paper: Color.FromRgb(0x18, 0x22, 0x1B),
                Ivory: Color.FromRgb(0xE6, 0xEF, 0xE4),
                Muted: Color.FromRgb(0x8F, 0xA4, 0x93),
                Brass: Color.FromRgb(0xA3, 0xB8, 0x7A),
                Ember: Color.FromRgb(0xC4, 0x7A, 0x4A),
                Rule: Color.FromRgb(0x2E, 0x3C, 0x32),
                Plate: Color.FromRgb(0x24, 0x30, 0x27),
                Well: Color.FromRgb(0x12, 0x1A, 0x14),
                Placeholder: Color.FromArgb(0x88, 0x8F, 0xA4, 0x93),
                Halo: Color.FromRgb(0x2E, 0x42, 0x32),
                OnHalo: Color.FromRgb(0xE8, 0xF2, 0xE6));
        }

        // InkBlack — current default
        return new ThemePalette(
            Ink: Color.FromRgb(0x14, 0x11, 0x0F),
            Paper: Color.FromRgb(0x22, 0x1C, 0x17),
            Ivory: Color.FromRgb(0xF3, 0xEB, 0xE0),
            Muted: Color.FromRgb(0xB3, 0x9F, 0x8C),
            Brass: Color.FromRgb(0xC8, 0xA5, 0x6A),
            Ember: Color.FromRgb(0xD4, 0x56, 0x3B),
            Rule: Color.FromRgb(0x3F, 0x36, 0x2E),
            Plate: Color.FromRgb(0x2A, 0x23, 0x1D),
            Well: Color.FromRgb(0x19, 0x15, 0x11),
            Placeholder: Color.FromArgb(0x66, 0xB3, 0x9F, 0x8C),
            Halo: Color.FromRgb(0x4A, 0x3C, 0x32),
            OnHalo: Color.FromRgb(0xF3, 0xEB, 0xE0));
    }

    public static ThemePalette Apply(string? themeId)
    {
        var palette = GetPalette(themeId);
        var app = Application.Current;
        if (app is null)
        {
            return palette;
        }

        SetBrush(app, "InkBrush", palette.Ink);
        SetBrush(app, "PaperBrush", palette.Paper);
        SetBrush(app, "IvoryBrush", palette.Ivory);
        SetBrush(app, "MutedBrush", palette.Muted);
        SetBrush(app, "BrassBrush", palette.Brass);
        SetBrush(app, "EmberBrush", palette.Ember);
        SetBrush(app, "RuleBrush", palette.Rule);
        SetBrush(app, "PlateBrush", palette.Plate);
        SetBrush(app, "WellBrush", palette.Well);
        SetBrush(app, "PlaceholderBrush", palette.Placeholder);
        SetBrush(app, "HaloBrush", palette.Halo);
        SetBrush(app, "OnHaloBrush", palette.OnHalo);
        return palette;
    }

    private static void SetBrush(Application app, string key, Color color)
    {
        // Replace (do not mutate frozen brushes) so DynamicResource listeners refresh.
        app.Resources[key] = new SolidColorBrush(color);
    }
}
