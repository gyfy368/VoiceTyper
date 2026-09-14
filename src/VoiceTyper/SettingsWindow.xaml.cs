using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VoiceTyper.Services;

namespace VoiceTyper;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _draft;
    private readonly string _openingTheme;
    private bool _suppressThemePreview;

    public AppSettings? Result { get; private set; }

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        _draft = current.Clone();
        _openingTheme = SettingsStore.Normalize(current.Clone()).ColorTheme;
        LoadUi(_draft);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Soft entrance — slight fade + scale, not flashy.
        Opacity = 0;
        var root = (FrameworkElement)Content;
        root.RenderTransformOrigin = new Point(0.5, 0.42);
        root.RenderTransform = new ScaleTransform(0.97, 0.97);

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        };
        var scaleX = new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(380))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        };
        var scaleY = new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(380))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        };

        BeginAnimation(OpacityProperty, fade);
        root.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        root.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);

        // Ensure wheel / thumb work even when focus is on a CheckBox.
        SettingsScroll.Focus();
        CaptionTheme.Apply(this, ThemeService.GetPalette(ReadTag(ColorThemeBox, ColorThemes.InkBlack)).Paper);
    }

    private void LoadUi(AppSettings s)
    {
        _suppressThemePreview = true;
        try
        {
            SelectByTag(CloseBehaviorBox, s.CloseBehavior);
            SelectByTag(DockSideBox, s.DockSide);
            SelectByTag(ColorThemeBox, s.ColorTheme);
            SelectByTag(PaperSizeBox, s.PaperSizePreset);
            ShowInTaskbarBox.IsChecked = s.ShowInTaskbar;
            TopmostBox.IsChecked = s.Topmost;
            CollapseOnDeactivateBox.IsChecked = s.CollapseOnDeactivate;
            VadAutoStopBox.IsChecked = s.VadAutoStop;
            AutoCopyBox.IsChecked = s.AutoCopy;
            CollapseAfterDoneBox.IsChecked = s.CollapseAfterDone;
        }
        finally
        {
            _suppressThemePreview = false;
        }
    }

    private AppSettings ReadUi()
    {
        var preset = ReadTag(PaperSizeBox, PaperSize.Presets.Medium);
        double width = _draft.PaperWidth;
        double height = _draft.PaperHeight;
        if (preset is PaperSize.Presets.Small or PaperSize.Presets.Medium or PaperSize.Presets.Large)
        {
            (width, height) = PaperSize.FromPreset(preset);
        }
        else
        {
            (width, height) = PaperSize.Clamp(width, height);
            preset = PaperSize.MatchPreset(width, height);
        }

        return new AppSettings
        {
            CloseBehavior = ReadTag(CloseBehaviorBox, CloseBehaviors.Exit),
            DockSide = ReadTag(DockSideBox, DockSides.Right),
            ShadowMode = ShadowModes.Off,
            ColorTheme = ReadTag(ColorThemeBox, ColorThemes.InkBlack),
            ShowInTaskbar = ShowInTaskbarBox.IsChecked == true,
            Topmost = TopmostBox.IsChecked == true,
            CollapseOnDeactivate = CollapseOnDeactivateBox.IsChecked == true,
            VadAutoStop = VadAutoStopBox.IsChecked == true,
            AutoCopy = AutoCopyBox.IsChecked == true,
            CollapseAfterDone = CollapseAfterDoneBox.IsChecked == true,
            AnimationsEnabled = true,
            PaperSizePreset = preset,
            PaperWidth = width,
            PaperHeight = height,
            PaperLeft = _draft.PaperLeft,
            PaperTop = _draft.PaperTop
        };
    }

    private void OnThemePreviewChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressThemePreview || !IsLoaded)
        {
            return;
        }

        ThemeService.Apply(ReadTag(ColorThemeBox, ColorThemes.InkBlack));
        CaptionTheme.Apply(this, ThemeService.GetPalette(ReadTag(ColorThemeBox, ColorThemes.InkBlack)).Paper);
    }

    private static void SelectByTag(ComboBox box, string tag)
    {
        for (var i = 0; i < box.Items.Count; i++)
        {
            if (box.Items[i] is ComboBoxItem item &&
                string.Equals(Convert.ToString(item.Tag), tag, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedIndex = i;
                return;
            }
        }

        if (box.Items.Count > 0)
        {
            box.SelectedIndex = 0;
        }
    }

    private static string ReadTag(ComboBox box, string fallback)
    {
        if (box.SelectedItem is ComboBoxItem item && item.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
        {
            return tag;
        }

        return fallback;
    }

    /// <summary>
    /// Pixel-scroll the settings body. Handles wheel even when a child control has focus,
    /// and avoids logical-scroll / thumb desync from CanContentScroll.
    /// </summary>
    private void OnScrollPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollByWheel(SettingsScroll, e);
    }

    private void OnWindowPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        // If the cursor is over an open ComboBox dropdown, leave it alone.
        if (Mouse.DirectlyOver is DependencyObject d && IsInsideComboDropdown(d))
        {
            return;
        }

        ScrollByWheel(SettingsScroll, e);
    }

    private static void ScrollByWheel(ScrollViewer sv, MouseWheelEventArgs e)
    {
        if (sv.ScrollableHeight <= 0)
        {
            return;
        }

        // e.Delta is typically ±120 per notch; map to pixels for smooth feel.
        var offset = sv.VerticalOffset - e.Delta;
        sv.ScrollToVerticalOffset(Math.Clamp(offset, 0, sv.ScrollableHeight));
        e.Handled = true;
    }

    private static bool IsInsideComboDropdown(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is Popup || current.GetType().Name.Contains("PopupRoot", StringComparison.Ordinal))
            {
                return true;
            }

            current = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        Result = SettingsStore.Normalize(ReadUi());
        ThemeService.Apply(Result.ColorTheme);
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        // Revert live theme preview.
        ThemeService.Apply(_openingTheme);
        Result = null;
        DialogResult = false;
        Close();
    }
}
