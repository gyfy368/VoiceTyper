using System.IO;
using System.Text.Json;

namespace VoiceTyper.Services;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string DefaultPath =>
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return Normalize(loaded ?? new AppSettings());
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings, string? path = null)
    {
        path ??= DefaultPath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(Normalize(settings), JsonOptions);
        File.WriteAllText(path, json);
    }

    public static AppSettings Normalize(AppSettings settings)
    {
        settings.CloseBehavior = string.Equals(settings.CloseBehavior, CloseBehaviors.Tray, StringComparison.OrdinalIgnoreCase)
            ? CloseBehaviors.Tray
            : CloseBehaviors.Exit;
        settings.DockSide = string.Equals(settings.DockSide, DockSides.Left, StringComparison.OrdinalIgnoreCase)
            ? DockSides.Left
            : DockSides.Right;
        settings.ShadowMode = ShadowModes.Off;
        settings.AnimationsEnabled = true;
        settings.ColorTheme = NormalizeTheme(settings.ColorTheme);
        settings.CopyMode = string.Equals(settings.CopyMode, CopyModes.Entire, StringComparison.OrdinalIgnoreCase)
            ? CopyModes.Entire
            : CopyModes.NewOnly;
        settings.WindowOpacity = Math.Clamp(settings.WindowOpacity, 0.40, 1.0);
        settings.PaperSizePreset = PaperSize.NormalizePreset(settings.PaperSizePreset);

        if (settings.PaperSizePreset is PaperSize.Presets.Small or PaperSize.Presets.Medium or PaperSize.Presets.Large)
        {
            var (w, h) = PaperSize.FromPreset(settings.PaperSizePreset);
            settings.PaperWidth = w;
            settings.PaperHeight = h;
        }
        else
        {
            var (w, h) = PaperSize.Clamp(settings.PaperWidth, settings.PaperHeight);
            settings.PaperWidth = w;
            settings.PaperHeight = h;
            settings.PaperSizePreset = PaperSize.MatchPreset(w, h);
        }

        return settings;
    }

    private static string NormalizeTheme(string? theme)
    {
        if (string.Equals(theme, ColorThemes.WarmPaper, StringComparison.OrdinalIgnoreCase))
        {
            return ColorThemes.WarmPaper;
        }

        if (string.Equals(theme, ColorThemes.CoolGray, StringComparison.OrdinalIgnoreCase))
        {
            return ColorThemes.CoolGray;
        }

        if (string.Equals(theme, ColorThemes.Forest, StringComparison.OrdinalIgnoreCase))
        {
            return ColorThemes.Forest;
        }

        return ColorThemes.InkBlack;
    }
}
