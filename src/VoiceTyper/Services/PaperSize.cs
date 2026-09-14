namespace VoiceTyper.Services;

/// <summary>
/// Expanded paper window size: presets + clamped custom width/height.
/// </summary>
public static class PaperSize
{
    public const double DefaultWidth = 320;
    public const double DefaultHeight = 428;
    public const double MinWidth = 260;
    public const double MaxWidth = 560;
    public const double MinHeight = 360;
    public const double MaxHeight = 720;

    public static class Presets
    {
        public const string Small = "Small";
        public const string Medium = "Medium";
        public const string Large = "Large";
        public const string Custom = "Custom";
    }

    public static (double Width, double Height) Clamp(double width, double height)
    {
        return (
            Math.Clamp(width, MinWidth, MaxWidth),
            Math.Clamp(height, MinHeight, MaxHeight));
    }

    public static (double Width, double Height) FromPreset(string? preset)
    {
        if (string.Equals(preset, Presets.Small, StringComparison.OrdinalIgnoreCase))
        {
            return (280, 380);
        }

        if (string.Equals(preset, Presets.Large, StringComparison.OrdinalIgnoreCase))
        {
            return (400, 560);
        }

        // Medium / unknown → default medium
        return (DefaultWidth, DefaultHeight);
    }

    public static string NormalizePreset(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset) ||
            string.Equals(preset, Presets.Medium, StringComparison.OrdinalIgnoreCase))
        {
            return Presets.Medium;
        }

        if (string.Equals(preset, Presets.Small, StringComparison.OrdinalIgnoreCase))
        {
            return Presets.Small;
        }

        if (string.Equals(preset, Presets.Large, StringComparison.OrdinalIgnoreCase))
        {
            return Presets.Large;
        }

        if (string.Equals(preset, Presets.Custom, StringComparison.OrdinalIgnoreCase))
        {
            return Presets.Custom;
        }

        // Unknown → treat as custom so width/height clamping still applies.
        return Presets.Custom;
    }

    public static string MatchPreset(double width, double height)
    {
        var (w, h) = Clamp(width, height);
        if (Near(w, 280) && Near(h, 380))
        {
            return Presets.Small;
        }

        if (Near(w, DefaultWidth) && Near(h, DefaultHeight))
        {
            return Presets.Medium;
        }

        if (Near(w, 400) && Near(h, 560))
        {
            return Presets.Large;
        }

        return Presets.Custom;
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 0.5;
}
