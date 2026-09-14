using System.Windows.Media;

namespace VoiceTyper.Services;

public static class ThemeContrast
{
    public static double RelativeLuminance(Color c)
    {
        static double Linear(byte channel)
        {
            var s = channel / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
    }

    public static double Ratio(Color a, Color b)
    {
        var l1 = RelativeLuminance(a);
        var l2 = RelativeLuminance(b);
        var hi = Math.Max(l1, l2);
        var lo = Math.Min(l1, l2);
        return (hi + 0.05) / (lo + 0.05);
    }

    public static bool IsDark(Color c) => RelativeLuminance(c) < 0.45;
}
