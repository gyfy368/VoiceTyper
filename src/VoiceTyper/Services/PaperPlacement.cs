using System.Windows;

namespace VoiceTyper.Services;

public static class PaperPlacement
{
    public const double DockMargin = 10;
    public const double MinVisible = 48;

    public static bool SnapAfterDrag(bool expanded) => !expanded;

    public static (double Left, double Top) Dock(
        bool dockRight, double width, double height, Rect work, double currentTop)
    {
        var left = dockRight ? work.Right - width - DockMargin : work.Left + DockMargin;
        var minTop = work.Top + DockMargin;
        var maxTop = work.Bottom - height - DockMargin;
        if (maxTop < minTop)
        {
            maxTop = minTop;
        }

        return (left, Math.Clamp(currentTop, minTop, maxTop));
    }

    public static (double Left, double Top) ClampFree(
        double left, double top, double width, double height, Rect work)
    {
        var minLeft = work.Left + MinVisible - width;
        var maxLeft = work.Right - MinVisible;
        var minTop = work.Top + DockMargin;
        var maxTop = work.Bottom - MinVisible;
        if (maxLeft < minLeft)
        {
            maxLeft = minLeft;
        }

        if (maxTop < minTop)
        {
            maxTop = minTop;
        }

        return (Math.Clamp(left, minLeft, maxLeft), Math.Clamp(top, minTop, maxTop));
    }

    public static (double Left, double Top) ExpandTarget(
        bool hasSaved,
        double? savedLeft,
        double? savedTop,
        bool dockRight,
        double width,
        double height,
        Rect work,
        double currentTop)
    {
        if (hasSaved && savedLeft is double sl && savedTop is double st)
        {
            return ClampFree(sl, st, width, height, work);
        }

        return Dock(dockRight, width, height, work, currentTop);
    }

    public static bool ShouldPersist(bool settingsHadPosition, bool userDragged) =>
        settingsHadPosition || userDragged;
}
