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
    private bool _opacityDragging;
    private double _opacityVelocity;
    private double _lastOpacityValue;
    private long _lastOpacityTicks;

    public AppSettings? Result { get; private set; }

    public event Action<double>? WindowOpacityPreview;

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        _draft = current.Clone();
        _openingTheme = SettingsStore.Normalize(current.Clone()).ColorTheme;
        LoadUi(_draft);
        OpacitySlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnOpacityDragStarted), true);
        OpacitySlider.AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnOpacityDragDelta), true);
        OpacitySlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnOpacityDragCompleted), true);
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
            ShowCopyToastBox.IsChecked = s.ShowCopyToast;
            SelectByTag(CopyModeBox, s.CopyMode);
            CollapseAfterDoneBox.IsChecked = s.CollapseAfterDone;
            OpacitySlider.Value = Math.Clamp(s.WindowOpacity, 0.40, 1.0);
            OpacityValueText.Text = FormatOpacity(OpacitySlider.Value);
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
            ShowCopyToast = ShowCopyToastBox.IsChecked == true,
            CopyMode = ReadTag(CopyModeBox, CopyModes.NewOnly),
            CollapseAfterDone = CollapseAfterDoneBox.IsChecked == true,
            WindowOpacity = Math.Clamp(OpacitySlider.Value, 0.40, 1.0),
            AnimationsEnabled = true,
            PaperSizePreset = preset,
            PaperWidth = width,
            PaperHeight = height,
            PaperLeft = _draft.PaperLeft,
            PaperTop = _draft.PaperTop
        };
    }

    private void OnOpacitySliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || OpacityValueText is null)
        {
            return;
        }

        OpacityValueText.Text = FormatOpacity(e.NewValue);
        if (_opacityDragging)
        {
            var now = Environment.TickCount64;
            var dt = Math.Max(1, now - _lastOpacityTicks) / 1000.0;
            _opacityVelocity = (e.NewValue - _lastOpacityValue) / dt;
            _lastOpacityValue = e.NewValue;
            _lastOpacityTicks = now;
        }

        if (!_suppressThemePreview)
        {
            WindowOpacityPreview?.Invoke(Math.Clamp(e.NewValue, 0.40, 1.0));
        }
    }

    private void OnOpacityDragStarted(object sender, DragStartedEventArgs e)
    {
        _opacityDragging = true;
        _opacityVelocity = 0;
        _lastOpacityValue = OpacitySlider.Value;
        _lastOpacityTicks = Environment.TickCount64;
    }

    private void OnOpacityDragDelta(object sender, DragDeltaEventArgs e)
    {
        // Velocity is sampled from ValueChanged while dragging.
    }

    private void OnOpacityDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _opacityDragging = false;
        var overshoot = Math.Clamp(_opacityVelocity * 0.06, -0.08, 0.08);
        var raw = Math.Clamp(OpacitySlider.Value + overshoot, 0.40, 1.0);
        var snapped = Math.Clamp(Math.Round(raw / 0.05) * 0.05, 0.40, 1.0);
        var spring = new DoubleAnimation(OpacitySlider.Value, snapped, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new BackEase { Amplitude = 0.28, EasingMode = EasingMode.EaseOut }
        };
        spring.Completed += (_, _) =>
        {
            OpacitySlider.BeginAnimation(Slider.ValueProperty, null);
            OpacitySlider.Value = snapped;
            WindowOpacityPreview?.Invoke(snapped);
        };
        OpacitySlider.BeginAnimation(Slider.ValueProperty, spring);
    }

    private static string FormatOpacity(double value) =>
        $"{(int)Math.Round(Math.Clamp(value, 0.40, 1.0) * 100)}%";

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
