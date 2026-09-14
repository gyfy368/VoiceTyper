using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using VoiceTyper.Services;

namespace VoiceTyper;

public partial class MainWindow : Window
{
    private const double CapsuleWidth = 188;
    private const double CapsuleHeight = 56;
    /// <summary>Expand is snappier; collapse settles longer (asymmetric ease-out).</summary>
    private const int ExpandAnimMs = 360;
    private const int CollapseAnimMs = 480;
    /// <summary>Content opacity finishes slightly ahead of width/height settle.</summary>
    private const int ContentFadeLeadMs = 70;
    private const int WaveSlots = 96;
    private const int MaxRecordSeconds = 60;
    private const int MicSplitMs = 400;
    private const double DragThresholdPx = 6;

    private readonly SessionController _session = new();
    private readonly Recorder _recorder = new();
    private readonly Transcriber _transcriber = new();
    private readonly ClipboardService _clipboard = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly float[] _wave = new float[WaveSlots];
    private SolidColorBrush _ember = new(Color.FromRgb(0xD4, 0x56, 0x3B));
    private SolidColorBrush _halo = new(Color.FromRgb(0x4A, 0x3C, 0x32));
    private SolidColorBrush _brass = new(Color.FromRgb(0xC8, 0xA5, 0x6A));

    private AppSettings _settings = new();
    private VadMonitor? _vad;
    private bool _expanded;
    private bool _dockRight = true;
    private bool _allowClose;
    private bool _animating;
    private int _waveWrite;
    private int _modalDepth;
    private CancellationTokenSource? _downloadCts;
    private Point _pressPoint;
    private bool _pressDragging;
    private bool _pressActive;
    private SettingsWindow? _settingsWindow;
    private bool _suppressSizePersist;
    private bool _paperResizeHooked;
    private bool _dualMic;
    private bool _micSplitAnimating;
    private MicSlot _recordSlot = MicSlot.Single;
    private double? _sessionPaperLeft;
    private double? _sessionPaperTop;

    private double PaperWidth => _settings.PaperWidth;
    private double PaperHeight => _settings.PaperHeight;

    public MainWindow()
    {
        InitializeComponent();
        _settings = SettingsStore.Load();
        _sessionPaperLeft = _settings.PaperLeft;
        _sessionPaperTop = _settings.PaperTop;
        Icon = TrayIconFactory.Create();
        TrayIcon.IconSource = TrayIconFactory.Create();
        _tick.Tick += OnTick;
        _recorder.LevelChanged += OnLevel;
        _recorder.SamplesAvailable += OnSamples;
        TranscriptBox.TextChanged += OnTranscriptChanged;
        Loaded += OnLoaded;
        Deactivated += OnDeactivated;
        Closing += OnClosing;
        SizeChanged += OnWindowSizeChanged;
        SourceInitialized += (_, _) => PlaceCapsule();
        ApplySettings(_settings, snapDock: true);
        RefreshModelHint();
        TryWarmupEngine();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PlaceCapsule();
        ApplyCollapsedVisuals(animate: false);
        ApplySettings(_settings, snapDock: true);
        SyncMicLayout(animate: false);
    }

    private void PlaceCapsule()
    {
        var work = SystemParameters.WorkArea;
        Left = _dockRight ? work.Right - CapsuleWidth - 10 : work.Left + 10;
        Top = work.Top + work.Height * 0.38;
    }

    private void ApplySettings(AppSettings settings, bool snapDock)
    {
        _settings = SettingsStore.Normalize(settings.Clone());
        ShowInTaskbar = _settings.ShowInTaskbar;
        Topmost = _settings.Topmost;
        _dockRight = _settings.DockRight;
        ApplyThemePalette(ThemeService.Apply(_settings.ColorTheme));
        ApplyShadow(false);
        if (snapDock && !_expanded)
        {
            SnapToEdge(Width > 0 ? Width : CapsuleWidth, Height > 0 ? Height : CapsuleHeight);
        }

        HintText.Text = BuildIdleHint();
        if (HintSecondary is not null)
        {
            HintSecondary.Visibility = Visibility.Visible;
            HintSecondary.Text = "行为可在设置里改";
        }

        if (_session.Phase is AppPhase.Idle or AppPhase.Done)
        {
            StatusText.Text = BuildIdleStatus();
        }

        if (_expanded && !_animating)
        {
            var (w, h) = PaperSize.Clamp(_settings.PaperWidth, _settings.PaperHeight);
            _suppressSizePersist = true;
            try
            {
                Width = w;
                Height = h;
                ApplyPaperPosition(w, h);
            }
            finally
            {
                _suppressSizePersist = false;
            }
        }
    }

    private void ApplyThemePalette(ThemePalette palette)
    {
        _ember = new SolidColorBrush(palette.Ember);
        _halo = new SolidColorBrush(palette.Halo);
        _brass = new SolidColorBrush(palette.Brass);
        if (_session.Phase == AppPhase.Recording)
        {
            CapsuleDot.Fill = _ember;
            FillActiveHalo(_ember);
        }
        else if (_session.Phase == AppPhase.Transcribing)
        {
            CapsuleDot.Fill = _brass;
        }
        else
        {
            CapsuleDot.Fill = _brass;
            FillIdleHalos();
        }
    }

    private string BuildIdleStatus()
    {
        return _settings.VadAutoStop
            ? "点麦克风开始 · 停顿后自动结束"
            : "点麦克风开始，再点一次结束";
    }

    private string BuildIdleHint()
    {
        if (_settings.AutoCopy)
        {
            return _settings.CollapseAfterDone
                ? "结果会复制到剪贴板，然后收成胶囊"
                : "结果会复制到剪贴板，窗口先留着方便改";
        }

        return "转写完成后可手动复制";
    }

    private void ApplyShadow(bool light)
    {
        if (light)
        {
            Shell.Effect = new DropShadowEffect
            {
                BlurRadius = 16,
                ShadowDepth = 4,
                Direction = 270,
                Opacity = 0.28,
                Color = Color.FromRgb(0, 0, 0)
            };
        }
        else
        {
            Shell.Effect = null;
        }
    }

    private void OnCapsuleMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _pressPoint = e.GetPosition(this);
        _pressDragging = false;
        _pressActive = true;
        CapsuleLayer.CaptureMouse();
        e.Handled = true;
    }

    private void OnCapsuleMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressActive || e.LeftButton != MouseButtonState.Pressed || _pressDragging)
        {
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _pressPoint.X) < DragThresholdPx &&
            Math.Abs(current.Y - _pressPoint.Y) < DragThresholdPx)
        {
            return;
        }

        _pressDragging = true;
        if (CapsuleLayer.IsMouseCaptured)
        {
            CapsuleLayer.ReleaseMouseCapture();
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // Ignore clicks that aren't a real drag.
        }

        var work = SystemParameters.WorkArea;
        _dockRight = Left + Width / 2 > work.Left + work.Width / 2;
        _settings.DockSide = _dockRight ? DockSides.Right : DockSides.Left;
        SnapToEdge(Width, Height);
        _pressActive = false;
    }

    private void OnCapsuleMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (CapsuleLayer.IsMouseCaptured)
        {
            CapsuleLayer.ReleaseMouseCapture();
        }

        var wasClick = _pressActive && !_pressDragging;
        _pressActive = false;
        _pressDragging = false;
        e.Handled = true;

        if (!wasClick || _animating)
        {
            return;
        }

        if (_expanded)
        {
            Collapse();
        }
        else
        {
            Expand();
        }
    }

    private void OnPaperChromeMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_expanded || e.ChangedButton != MouseButton.Left || _animating)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject origin)
        {
            if (FindParent<Button>(origin) is not null ||
                FindParent<TextBox>(origin) is not null)
            {
                return;
            }

            if (ReferenceEquals(origin, PaperResizeGrip))
            {
                return;
            }
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }

        RememberPaperPosition(persist: true);
        e.Handled = true;
    }

    private void OnShellRightClick(object sender, MouseButtonEventArgs e)
    {
        if (FindResource("CapsuleContextMenu") is ContextMenu menu)
        {
            menu.PlacementTarget = Shell;
            menu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void SnapToEdge(double width, double height)
    {
        var work = SystemParameters.WorkArea;
        var pos = PaperPlacement.Dock(_dockRight, width, height, work, Top);
        Left = pos.Left;
        Top = pos.Top;
    }

    private void ApplyPaperPosition(double width, double height)
    {
        var work = SystemParameters.WorkArea;
        var savedLeft = _settings.PaperLeft ?? _sessionPaperLeft;
        var savedTop = _settings.PaperTop ?? _sessionPaperTop;
        var hasSaved = savedLeft.HasValue && savedTop.HasValue;
        var pos = PaperPlacement.ExpandTarget(hasSaved, savedLeft, savedTop, _dockRight, width, height, work, Top);
        Left = pos.Left;
        Top = pos.Top;
    }

    private void RememberPaperPosition(bool persist)
    {
        if (!_expanded)
        {
            return;
        }

        var work = SystemParameters.WorkArea;
        var clamped = PaperPlacement.ClampFree(Left, Top, Width, Height, work);
        Left = clamped.Left;
        Top = clamped.Top;
        _sessionPaperLeft = Left;
        _sessionPaperTop = Top;
        if (!persist || !PaperPlacement.ShouldPersist(_settings.HasPaperPosition, userDragged: true))
        {
            return;
        }

        _settings.PaperLeft = Left;
        _settings.PaperTop = Top;
        SettingsStore.Save(_settings);
    }

    private void OnCollapseClick(object sender, RoutedEventArgs e) => Collapse();

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (!_settings.CollapseOnDeactivate || _modalDepth > 0 || _animating || _settingsWindow is not null)
        {
            return;
        }

        if (_expanded && !_session.IsBusy)
        {
            Collapse();
        }
    }

    private void Expand()
    {
        if (_expanded || _animating)
        {
            return;
        }

        var (w, h) = PaperSize.Clamp(PaperWidth, PaperHeight);
        AnimateShape(w, h, paper: true);
    }

    private void Collapse()
    {
        if (!_expanded || _animating || _session.IsBusy)
        {
            return;
        }

        RememberPaperSizeFromWindow();
        AnimateShape(CapsuleWidth, CapsuleHeight, paper: false);
    }

    private void AnimateShape(double width, double height, bool paper)
    {
        _suppressSizePersist = true;
        if (paper)
        {
            // Capsule locks MaxWidth/Height — lift caps before morphing to paper.
            MinWidth = PaperSize.MinWidth;
            MinHeight = PaperSize.MinHeight;
            MaxWidth = PaperSize.MaxWidth;
            MaxHeight = PaperSize.MaxHeight;
            ResizeMode = ResizeMode.CanResizeWithGrip;
        }
        else
        {
            ResizeMode = ResizeMode.NoResize;
        }

        _animating = true;
        // Asymmetric motion: expand snappier CubicEaseOut; collapse longer QuinticEaseOut settle.
        var durationMs = paper ? ExpandAnimMs : CollapseAnimMs;
        var duration = TimeSpan.FromMilliseconds(durationMs);
        IEasingFunction geometryEase = paper
            ? new CubicEase { EasingMode = EasingMode.EaseOut }
            : new QuinticEase { EasingMode = EasingMode.EaseOut };

        // Staged opacity: content fade finishes ~70ms ahead of geometry settle.
        var fadeMs = Math.Max(200, durationMs - ContentFadeLeadMs);
        var fadeDuration = TimeSpan.FromMilliseconds(fadeMs);
        var work = SystemParameters.WorkArea;
        double targetLeft;
        double targetTop;
        if (paper)
        {
            var savedLeft = _settings.PaperLeft ?? _sessionPaperLeft;
            var savedTop = _settings.PaperTop ?? _sessionPaperTop;
            var hasSaved = savedLeft.HasValue && savedTop.HasValue;
            (targetLeft, targetTop) = PaperPlacement.ExpandTarget(
                hasSaved, savedLeft, savedTop, _dockRight, width, height, work, Top);
        }
        else
        {
            (targetLeft, targetTop) = PaperPlacement.Dock(_dockRight, width, height, work, Top);
        }

        // Morph the shared shell (spatial continuity). Prefer EaseOut settle — no linear stop.
        // HoldEnd until we commit base values in the settle timer (avoids FillBehavior.Stop snap-back).
        BeginAnimation(WidthProperty, new DoubleAnimation(Width, width, duration) { EasingFunction = geometryEase });
        BeginAnimation(HeightProperty, new DoubleAnimation(Height, height, duration) { EasingFunction = geometryEase });
        BeginAnimation(LeftProperty, new DoubleAnimation(Left, targetLeft, duration) { EasingFunction = geometryEase });
        BeginAnimation(TopProperty, new DoubleAnimation(Top, targetTop, duration) { EasingFunction = geometryEase });

        // Soft corner morph toward target mid-flight (same shell, not a hard visual cut).
        var midCorner = paper ? 24.0 : 26.0;
        var endCorner = paper ? 22.0 : 28.0;
        Shell.CornerRadius = new CornerRadius(midCorner);
        var cornerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs * 0.55) };
        cornerTimer.Tick += (_, _) =>
        {
            cornerTimer.Stop();
            if (_animating)
            {
                Shell.CornerRadius = new CornerRadius(endCorner);
            }
        };
        cornerTimer.Start();

        // Content crossfade: paper out ahead on collapse; capsule out first on expand.
        var paperFade = new DoubleAnimation(PaperLayer.Opacity, paper ? 1 : 0, fadeDuration)
        {
            BeginTime = paper ? TimeSpan.FromMilliseconds(45) : TimeSpan.Zero,
            EasingFunction = new CubicEase
            {
                EasingMode = paper ? EasingMode.EaseOut : EasingMode.EaseIn
            }
        };
        var capsuleFade = new DoubleAnimation(CapsuleLayer.Opacity, paper ? 0 : 1, fadeDuration)
        {
            BeginTime = paper ? TimeSpan.Zero : TimeSpan.FromMilliseconds(55),
            EasingFunction = new CubicEase
            {
                EasingMode = paper ? EasingMode.EaseIn : EasingMode.EaseOut
            }
        };
        PaperLayer.BeginAnimation(OpacityProperty, paperFade);
        CapsuleLayer.BeginAnimation(OpacityProperty, capsuleFade);

        // Scale follow-through (not bounce): collapse 1→0.98→1; expand 0.985→1.
        var scaleAnim = new DoubleAnimationUsingKeyFrames();
        if (paper)
        {
            scaleAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.985, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(
                1.0,
                KeyTime.FromTimeSpan(duration),
                new CubicEase { EasingMode = EasingMode.EaseOut }));
        }
        else
        {
            scaleAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(
                0.98,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.62)),
                new QuinticEase { EasingMode = EasingMode.EaseOut }));
            scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(
                1.0,
                KeyTime.FromTimeSpan(duration),
                new CubicEase { EasingMode = EasingMode.EaseOut }));
        }

        ShellScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        ShellScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim.Clone());

        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            PaperLayer.BeginAnimation(OpacityProperty, null);
            CapsuleLayer.BeginAnimation(OpacityProperty, null);
            ShellScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ShellScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            ShellScale.ScaleX = 1;
            ShellScale.ScaleY = 1;
            Width = width;
            Height = height;
            Left = targetLeft;
            Top = targetTop;
            if (paper)
            {
                ApplyExpandedVisuals();
            }
            else
            {
                ApplyCollapsedVisuals(animate: false);
            }

            _animating = false;
            _suppressSizePersist = false;
        };
        timer.Start();
    }

    private void ApplyExpandedVisuals()
    {
        _expanded = true;
        _suppressSizePersist = true;
        try
        {
            var (w, h) = PaperSize.Clamp(PaperWidth, PaperHeight);
            MinWidth = PaperSize.MinWidth;
            MinHeight = PaperSize.MinHeight;
            MaxWidth = PaperSize.MaxWidth;
            MaxHeight = PaperSize.MaxHeight;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Width = w;
            Height = h;
            Shell.CornerRadius = new CornerRadius(22);
            PaperLayer.Opacity = 1;
            PaperLayer.IsHitTestVisible = true;
            CapsuleLayer.Opacity = 0;
            CapsuleLayer.IsHitTestVisible = false;
            ApplyPaperPosition(w, h);
            EnsurePaperResizeHook();
            SyncMicLayout(animate: false);
        }
        finally
        {
            _suppressSizePersist = false;
        }
    }

    private void ApplyCollapsedVisuals(bool animate)
    {
        _expanded = false;
        _suppressSizePersist = true;
        try
        {
            ResizeMode = ResizeMode.NoResize;
            MinWidth = CapsuleWidth;
            MinHeight = CapsuleHeight;
            MaxWidth = CapsuleWidth;
            MaxHeight = CapsuleHeight;
            if (!animate)
            {
                BeginAnimation(WidthProperty, null);
                BeginAnimation(HeightProperty, null);
                BeginAnimation(LeftProperty, null);
                BeginAnimation(TopProperty, null);
                ShellScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                ShellScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                ShellScale.ScaleX = 1;
                ShellScale.ScaleY = 1;
                Width = CapsuleWidth;
                Height = CapsuleHeight;
                SnapToEdge(CapsuleWidth, CapsuleHeight);
            }

            Shell.CornerRadius = new CornerRadius(28);
            PaperLayer.Opacity = 0;
            PaperLayer.IsHitTestVisible = false;
            CapsuleLayer.Opacity = 1;
            CapsuleLayer.IsHitTestVisible = true;
        }
        finally
        {
            _suppressSizePersist = false;
        }
    }

    private void EnsurePaperResizeHook()
    {
        if (_paperResizeHooked || PaperResizeGrip is null)
        {
            return;
        }

        PaperResizeGrip.PreviewMouseLeftButtonDown += OnPaperResizeDown;
        PaperResizeGrip.PreviewMouseMove += OnPaperResizeMove;
        PaperResizeGrip.PreviewMouseLeftButtonUp += OnPaperResizeUp;
        _paperResizeHooked = true;
    }

    private Point _resizeOrigin;
    private Size _resizeStart;
    private bool _resizingPaper;

    private void OnPaperResizeDown(object sender, MouseButtonEventArgs e)
    {
        if (!_expanded || _animating)
        {
            return;
        }

        _resizingPaper = true;
        _resizeOrigin = e.GetPosition(this);
        _resizeStart = new Size(Width, Height);
        PaperResizeGrip.CaptureMouse();
        e.Handled = true;
    }

    private void OnPaperResizeMove(object sender, MouseEventArgs e)
    {
        if (!_resizingPaper || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var pos = e.GetPosition(this);
        var (w, h) = PaperSize.Clamp(
            _resizeStart.Width + (pos.X - _resizeOrigin.X),
            _resizeStart.Height + (pos.Y - _resizeOrigin.Y));
        Width = w;
        Height = h;
        e.Handled = true;
    }

    private void OnPaperResizeUp(object sender, MouseButtonEventArgs e)
    {
        if (!_resizingPaper)
        {
            return;
        }

        _resizingPaper = false;
        if (PaperResizeGrip.IsMouseCaptured)
        {
            PaperResizeGrip.ReleaseMouseCapture();
        }

        RememberPaperSizeFromWindow(persist: true);
        e.Handled = true;
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_suppressSizePersist || !_expanded || _animating || _resizingPaper)
        {
            return;
        }

        RememberPaperSizeFromWindow(persist: true);
    }

    private void RememberPaperSizeFromWindow(bool persist = false)
    {
        if (!_expanded)
        {
            return;
        }

        var (w, h) = PaperSize.Clamp(Width, Height);
        _settings.PaperWidth = w;
        _settings.PaperHeight = h;
        _settings.PaperSizePreset = PaperSize.MatchPreset(w, h);
        if (persist)
        {
            SettingsStore.Save(_settings);
        }
    }

    private async void OnMicClick(object sender, RoutedEventArgs e)
    {
        await HandleMicActionAsync(MicSlot.Single);
    }

    private async void OnNewSentenceClick(object sender, RoutedEventArgs e)
    {
        await HandleMicActionAsync(MicSlot.NewSentence);
    }

    private async void OnContinueClick(object sender, RoutedEventArgs e)
    {
        await HandleMicActionAsync(MicSlot.Continue);
    }

    private async Task HandleMicActionAsync(MicSlot slot)
    {
        if (_session.Phase == AppPhase.Transcribing)
        {
            return;
        }

        if (_session.Phase == AppPhase.Recording)
        {
            if (slot != _recordSlot && _recordSlot != MicSlot.Single)
            {
                return;
            }

            await StopAndTranscribeAsync();
            return;
        }

        _recordSlot = slot;
        await StartRecordingAsync();
        if (slot == MicSlot.NewSentence && _session.Phase == AppPhase.Recording)
        {
            TranscriptBox.Text = string.Empty;
        }
    }

    private async Task StartRecordingAsync()
    {
        if (!_session.TryStartRecording())
        {
            return;
        }

        if (!ModelLocator.TryResolve(out _, out var modelError))
        {
            _session.ReturnToIdle();
            _recordSlot = MicSlot.Single;
            SetStatus(modelError, error: true);
            DownloadButton.Visibility = Visibility.Visible;
            if (!_expanded)
            {
                Expand();
            }

            return;
        }

        Array.Clear(_wave);
        _waveWrite = 0;
        try
        {
            if (_vad is not null)
            {
                _vad.UtteranceEnded -= OnVadStop;
                _vad.Dispose();
                _vad = null;
            }

            if (_settings.VadAutoStop)
            {
                ModelLocator.TryResolve(out var paths, out _);
                _vad = new VadMonitor(paths.SileroVad);
                _vad.UtteranceEnded += OnVadStop;
            }

            _recorder.Start();
        }
        catch (Exception ex)
        {
            CrashLog.Write("start-recording", ex);
            _session.ReturnToIdle();
            _recordSlot = MicSlot.Single;
            SetStatus(ExceptionText.ForUser(ex) + "\n" + Recorder.DescribeDevices(), error: true);
            if (!_expanded)
            {
                Expand();
            }

            return;
        }

        if (!_expanded)
        {
            Expand();
        }

        SyncMicLayout(animate: true);
        SetRecordingVisual(true);
        SetStatus(_settings.VadAutoStop
            ? "正在听…停一下就会停录，也可再点麦克风"
            : "正在听…再说一次点麦克风结束");
        CapsuleLabel.Text = "录音中";
        CapsuleDot.Fill = _ember;
        _tick.Start();
        await Task.CompletedTask;
    }

    private void OnVadStop()
    {
        Dispatcher.BeginInvoke(async () =>
        {
            if (_settings.VadAutoStop && _session.Phase == AppPhase.Recording)
            {
                await StopAndTranscribeAsync();
            }
        });
    }

    private void OnSamples(float[] samples)
    {
        _vad?.Push(samples);
    }

    private void OnLevel(float peak)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _wave[_waveWrite % WaveSlots] = Math.Clamp(peak, 0, 1);
            _waveWrite++;
        });
    }

    private async Task StopAndTranscribeAsync()
    {
        if (!_session.TryBeginTranscribing())
        {
            return;
        }

        // Detach VAD from the audio callback BEFORE stopping the recorder.
        // Disposing Silero while WaveIn still pushes samples races the native handle
        // (nullptr / SEH / NullReference on the callback thread).
        var vad = _vad;
        _vad = null;
        if (vad is not null)
        {
            vad.UtteranceEnded -= OnVadStop;
        }

        _tick.Stop();

        try
        {
            SetRecordingVisual(false);
            SetTranscribingVisual(true);
            SetMicButtonsEnabled(false);
            PlaceScanOnActiveSlot();
            CapsuleLabel.Text = "转写中";
            CapsuleDot.Fill = _brass;
            SetStatus("本地转写中，先别关…");
        }
        catch (Exception ex)
        {
            CrashLog.Write("ui-before-stop", ex);
            SafeDisposeVad(vad);
            FinishError(ExceptionText.ForUser(ex));
            return;
        }

        float[] audio;
        try
        {
            // Stop capturing first so OnSamples cannot touch VAD anymore.
            audio = _recorder.Stop();
        }
        catch (Exception ex)
        {
            CrashLog.Write("recorder-stop", ex);
            SafeDisposeVad(vad);
            FinishError(ExceptionText.ForUser(ex));
            return;
        }

        SafeDisposeVad(vad);

        string text;
        try
        {
            text = await Task.Run(() => _transcriber.Transcribe(audio));
        }
        catch (Exception ex)
        {
            CrashLog.Write("transcribe", ex);
            FinishError(ExceptionText.ForUser(ex));
            return;
        }

        try
        {
            _session.TryMarkDone();
            SetTranscribingVisual(false);
            SetMicButtonsEnabled(true);
            var existing = TranscriptBox.Text;
            TranscriptBox.Text = TranscriptJoin.Apply(existing, text, MicChrome.AppendNext(_recordSlot));
            _recordSlot = MicSlot.Single;
            PlayTextEntrance();
            SyncMicLayout(animate: true);

            var copied = false;
            if (_settings.AutoCopy)
            {
                if (!_clipboard.TryCopy(TranscriptBox.Text, out var message))
                {
                    SetStatus(message);
                    CapsuleLabel.Text = "没听清";
                    CapsuleDot.Fill = _brass;
                    return;
                }

                copied = true;
            }

            FinishAfterTranscript(copied);
        }
        catch (Exception ex)
        {
            CrashLog.Write("ui-after-transcribe", ex);
            FinishError(ExceptionText.ForUser(ex));
        }
    }

    private static void SafeDisposeVad(VadMonitor? vad)
    {
        if (vad is null)
        {
            return;
        }

        try
        {
            vad.Dispose();
        }
        catch
        {
            // Native teardown can race; ignore.
        }
    }

    /// <summary>
    /// After transcript is ready: optionally auto-copied already.
    /// CollapseAfterDone off (default) keeps paper open for editing.
    /// </summary>
    private void FinishAfterTranscript(bool copied)
    {
        CapsuleDot.Fill = _brass;

        if (_settings.CollapseAfterDone)
        {
            if (copied)
            {
                SetStatus("已复制，可粘贴");
                CapsuleLabel.Text = "已复制";
                ScheduleCapsuleIdleLabel();
            }
            else
            {
                SetStatus("转写完成");
                CapsuleLabel.Text = "说一句";
            }

            Collapse();
            return;
        }

        // Stay expanded for editing.
        CapsuleLabel.Text = "说一句";
        if (copied)
        {
            SetStatus("已复制，可继续改字");
        }
        else
        {
            SetStatus("转写完成，可手动复制");
        }

        HintText.Text = BuildIdleHint();
        if (HintSecondary is not null)
        {
            HintSecondary.Visibility = Visibility.Visible;
        }
    }

    private void ScheduleCapsuleIdleLabel()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_session.Phase is AppPhase.Done or AppPhase.Idle)
            {
                CapsuleLabel.Text = "说一句";
                CapsuleDot.Fill = _brass;
            }
        };
        timer.Start();
    }

    private void FinishError(string message)
    {
        _session.ReturnToIdle();
        SetTranscribingVisual(false);
        SetRecordingVisual(false);
        SetMicButtonsEnabled(true);
        _recordSlot = MicSlot.Single;
        SyncMicLayout(animate: true);
        SetStatus(message, error: true);
        CapsuleLabel.Text = "出错了";
        CapsuleDot.Fill = _ember;
        if (!_expanded)
        {
            Expand();
        }
    }

    private bool _pulseRunning;
    private bool _scanRunning;

    private void SetRecordingVisual(bool on)
    {
        try
        {
            if (on)
            {
                FillActiveHalo(_ember);
                PulseRing.Opacity = 0.48;
                PlacePulseOnActiveSlot();
                if (TryGetStoryboard("PulseStory", out var pulse) &&
                    pulse is not null)
                {
                    pulse.Begin(this, true);
                    _pulseRunning = true;
                }
            }
            else
            {
                if (_pulseRunning &&
                    TryGetStoryboard("PulseStory", out var pulse) &&
                    pulse is not null)
                {
                    pulse.Stop(this);
                }

                _pulseRunning = false;
                PulseRing.Opacity = 0;
                PulseScale.ScaleX = 1;
                PulseScale.ScaleY = 1;
                FillIdleHalos();
            }
        }
        catch
        {
            _pulseRunning = false;
            if (PulseRing is not null)
            {
                PulseRing.Opacity = 0;
            }

            if (MicHalo is not null)
            {
                MicHalo.Fill = on ? _ember : _halo;
            }
        }
    }

    private void SetTranscribingVisual(bool on)
    {
        try
        {
            if (on)
            {
                ScanArc.Opacity = 1;
                PlaceScanOnActiveSlot();
                if (TryGetStoryboard("ScanStory", out var scan) &&
                    scan is not null)
                {
                    scan.Begin(this, true);
                    _scanRunning = true;
                }
            }
            else
            {
                if (_scanRunning &&
                    TryGetStoryboard("ScanStory", out var scan) &&
                    scan is not null)
                {
                    scan.Stop(this);
                }

                _scanRunning = false;
                ScanArc.Opacity = 0;
            }
        }
        catch
        {
            _scanRunning = false;
            if (ScanArc is not null)
            {
                ScanArc.Opacity = on ? 1 : 0;
            }
        }
    }

    private bool TryGetStoryboard(string key, out Storyboard? storyboard)
    {
        storyboard = null;
        try
        {
            if (FindResource(key) is Storyboard sb)
            {
                storyboard = sb;
                return true;
            }
        }
        catch
        {
            // Resource missing or not a Storyboard.
        }

        return false;
    }

    private void PlayTextEntrance()
    {
        TranscriptBox.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private void OnTranscriptChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        PlaceholderText.Visibility = string.IsNullOrWhiteSpace(TranscriptBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (_session.Phase is AppPhase.Idle or AppPhase.Done)
        {
            SyncMicLayout(animate: _expanded && !_animating);
        }
    }

    private bool HasTranscript => !string.IsNullOrWhiteSpace(TranscriptBox.Text);

    private void SyncMicLayout(bool animate)
    {
        var wantDual = MicChrome.UseDualLayout(_session.Phase, HasTranscript, _recordSlot);
        UpdateDualEnabledState();
        if (wantDual == _dualMic)
        {
            PlacePulseOnActiveSlot();
            PlaceScanOnActiveSlot();
            return;
        }

        _dualMic = wantDual;
        if (animate && _expanded && !_micSplitAnimating)
        {
            AnimateMicSplit(wantDual);
        }
        else
        {
            ApplyMicLayout(wantDual);
        }
    }

    private void ApplyMicLayout(bool dual)
    {
        NewSentenceShift.BeginAnimation(TranslateTransform.XProperty, null);
        ContinueShift.BeginAnimation(TranslateTransform.XProperty, null);
        SingleMicHost.BeginAnimation(OpacityProperty, null);
        DualMicHost.BeginAnimation(OpacityProperty, null);
        NewSentenceShift.X = dual ? 0 : 36;
        ContinueShift.X = dual ? 0 : -36;
        SingleMicHost.Opacity = dual ? 0 : 1;
        DualMicHost.Opacity = dual ? 1 : 0;
        SingleMicHost.IsHitTestVisible = !dual;
        DualMicHost.IsHitTestVisible = dual;
        PlacePulseOnActiveSlot();
        PlaceScanOnActiveSlot();
    }

    private void AnimateMicSplit(bool toDual)
    {
        _micSplitAnimating = true;
        var duration = TimeSpan.FromMilliseconds(MicSplitMs);
        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };

        DualMicHost.Visibility = Visibility.Visible;
        SingleMicHost.Visibility = Visibility.Visible;
        DualMicHost.IsHitTestVisible = toDual;
        SingleMicHost.IsHitTestVisible = !toDual;
        PlacePulseOnActiveSlot();
        PlaceScanOnActiveSlot();

        if (toDual)
        {
            NewSentenceShift.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(36, 0, duration) { EasingFunction = ease });
            ContinueShift.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(-36, 0, duration) { EasingFunction = ease });
            DualMicHost.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, duration)
                {
                    BeginTime = TimeSpan.FromMilliseconds(70),
                    EasingFunction = ease
                });
            SingleMicHost.BeginAnimation(OpacityProperty,
                new DoubleAnimation(SingleMicHost.Opacity, 0, TimeSpan.FromMilliseconds(240))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                });
        }
        else
        {
            NewSentenceShift.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(NewSentenceShift.X, 36, duration) { EasingFunction = ease });
            ContinueShift.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(ContinueShift.X, -36, duration) { EasingFunction = ease });
            DualMicHost.BeginAnimation(OpacityProperty,
                new DoubleAnimation(DualMicHost.Opacity, 0, TimeSpan.FromMilliseconds(240))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                });
            SingleMicHost.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, duration)
                {
                    BeginTime = TimeSpan.FromMilliseconds(80),
                    EasingFunction = ease
                });
        }

        var timer = new DispatcherTimer { Interval = duration + TimeSpan.FromMilliseconds(80) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ApplyMicLayout(toDual);
            _micSplitAnimating = false;
        };
        timer.Start();
    }

    private void UpdateDualEnabledState()
    {
        var idle = MicChrome.CanUseIdleActions(_session.Phase, _recordSlot);
        var recording = _session.Phase == AppPhase.Recording;
        if (NewSentenceButton is not null)
        {
            NewSentenceButton.IsEnabled = idle || (recording && _recordSlot == MicSlot.NewSentence);
            NewSentenceButton.Opacity = NewSentenceButton.IsEnabled ? 1 : 0.38;
        }

        if (ContinueButton is not null)
        {
            ContinueButton.IsEnabled = idle || (recording && _recordSlot == MicSlot.Continue);
            ContinueButton.Opacity = ContinueButton.IsEnabled ? 1 : 0.38;
        }

        if (MicButton is not null)
        {
            MicButton.IsEnabled = idle || (recording && _recordSlot == MicSlot.Single);
        }
    }

    private void SetMicButtonsEnabled(bool enabled)
    {
        if (enabled)
        {
            UpdateDualEnabledState();
            return;
        }

        MicButton.IsEnabled = false;
        if (NewSentenceButton is not null)
        {
            NewSentenceButton.IsEnabled = false;
            NewSentenceButton.Opacity = 0.38;
        }

        if (ContinueButton is not null)
        {
            ContinueButton.IsEnabled = false;
            ContinueButton.Opacity = 0.38;
        }
    }

    private void FillIdleHalos()
    {
        if (MicHalo is not null)
        {
            MicHalo.Fill = _halo;
        }

        if (NewSentenceHalo is not null)
        {
            NewSentenceHalo.Fill = _halo;
        }

        if (ContinueHalo is not null)
        {
            ContinueHalo.Fill = _halo;
        }
    }

    private void FillActiveHalo(Brush fill)
    {
        FillIdleHalos();
        switch (_recordSlot)
        {
            case MicSlot.NewSentence:
                if (NewSentenceHalo is not null)
                {
                    NewSentenceHalo.Fill = fill;
                }

                break;
            case MicSlot.Continue:
                if (ContinueHalo is not null)
                {
                    ContinueHalo.Fill = fill;
                }

                break;
            default:
                if (MicHalo is not null)
                {
                    MicHalo.Fill = fill;
                }

                break;
        }
    }

    private void PlacePulseOnActiveSlot()
    {
        if (PulseRing is null)
        {
            return;
        }

        if (_dualMic && _recordSlot == MicSlot.NewSentence)
        {
            PulseRing.HorizontalAlignment = HorizontalAlignment.Left;
            PulseRing.Margin = new Thickness(18, 8, 0, 0);
            PulseRing.Width = 70;
            PulseRing.Height = 70;
        }
        else if (_dualMic && _recordSlot == MicSlot.Continue)
        {
            PulseRing.HorizontalAlignment = HorizontalAlignment.Right;
            PulseRing.Margin = new Thickness(0, 8, 18, 0);
            PulseRing.Width = 70;
            PulseRing.Height = 70;
        }
        else
        {
            PulseRing.HorizontalAlignment = HorizontalAlignment.Center;
            PulseRing.Margin = new Thickness(0, 2, 0, 0);
            PulseRing.Width = 86;
            PulseRing.Height = 86;
        }
    }

    private void PlaceScanOnActiveSlot()
    {
        if (ScanArc is null)
        {
            return;
        }

        if (_dualMic && _recordSlot == MicSlot.NewSentence)
        {
            ScanArc.HorizontalAlignment = HorizontalAlignment.Left;
            ScanArc.Margin = new Thickness(24, 18, 0, 0);
        }
        else if (_dualMic && _recordSlot == MicSlot.Continue)
        {
            ScanArc.HorizontalAlignment = HorizontalAlignment.Right;
            ScanArc.Margin = new Thickness(0, 18, 24, 0);
        }
        else
        {
            ScanArc.HorizontalAlignment = HorizontalAlignment.Center;
            ScanArc.Margin = new Thickness(0, 16, 0, 0);
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_session.Phase != AppPhase.Recording)
        {
            return;
        }

        var elapsed = _recorder.Elapsed;
        TimerText.Text = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
        DrawWave();
        if (elapsed.TotalSeconds >= MaxRecordSeconds)
        {
            _ = StopAndTranscribeAsync();
        }
    }

    private void DrawWave()
    {
        var w = WaveCanvas.ActualWidth;
        var h = WaveCanvas.ActualHeight;
        if (w < 8 || h < 8)
        {
            return;
        }

        var mid = h / 2;
        var step = w / (WaveSlots - 1);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < WaveSlots; i++)
            {
                var idx = (_waveWrite + i) % WaveSlots;
                var amp = _wave[idx] * (h * 0.42);
                var x = i * step;
                var y = mid - amp;
                if (i == 0)
                {
                    ctx.BeginFigure(new Point(x, y), false, false);
                }
                else
                {
                    ctx.LineTo(new Point(x, y), true, false);
                }
            }
        }

        geometry.Freeze();
        WavePath.Data = geometry;
    }

    private void SetStatus(string text, bool error = false)
    {
        if (error && IsEnglishNullRef(text))
        {
            text = "出了点问题，请再点一次麦克风重试。若反复出现，请重启 VoiceTyper。";
        }

        StatusText.Text = text;
        try
        {
            StatusText.Foreground = error
                ? _ember
                : FindResource("MutedBrush") as Brush ?? _brass;
        }
        catch
        {
            StatusText.Foreground = _brass;
        }

        if (error)
        {
            HintText.Text = text;
            if (HintSecondary is not null)
            {
                HintSecondary.Visibility = Visibility.Collapsed;
            }

            _modalDepth++;
            try
            {
                if (!CrashLog.SuppressModalDialogs)
                {
                    MessageBox.Show(text, "VoiceTyper", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                _modalDepth--;
            }
        }
        else
        {
            HintText.Text = BuildIdleHint();
            if (HintSecondary is not null)
            {
                HintSecondary.Visibility = Visibility.Visible;
                HintSecondary.Text = "行为可在设置里改";
            }
        }
    }

    private static bool IsEnglishNullRef(string text) =>
        text.Contains("Object reference not set", StringComparison.OrdinalIgnoreCase) ||
        (text.Contains("Object reference", StringComparison.OrdinalIgnoreCase) &&
         text.Contains("instance of an object", StringComparison.OrdinalIgnoreCase));

    private static T? FindParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }

    private void RefreshModelHint()
    {
        if (ModelLocator.TryResolve(out _, out var error))
        {
            DownloadButton.Visibility = Visibility.Collapsed;
            HintText.Text = BuildIdleHint();
            if (HintSecondary is not null)
            {
                HintSecondary.Visibility = Visibility.Visible;
                HintSecondary.Text = "行为可在设置里改";
            }
        }
        else
        {
            DownloadButton.Visibility = Visibility.Visible;
            HintText.Text = error;
            if (HintSecondary is not null)
            {
                HintSecondary.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void TryWarmupEngine()
    {
        if (!ModelLocator.TryResolve(out var paths, out _))
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                _transcriber.EnsureLoaded(paths);
            }
            catch
            {
                // First real transcript will surface the error in Chinese.
            }
        });
    }

    private async void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        DownloadButton.IsEnabled = false;
        _downloadCts?.Cancel();
        _downloadCts = new CancellationTokenSource();
        var root = ModelLocator.ResolveModelsRoot();
        var progress = new Progress<DownloadProgress>(p =>
        {
            SetStatus($"正在下 {p.FileName}  {p.Percent}%", error: false);
            StatusText.Foreground = _brass;
        });
        try
        {
            await ModelDownloader.DownloadAsync(root, progress, _downloadCts.Token);
            SetStatus("模型好了。点麦克风就能说。");
            RefreshModelHint();
            TryWarmupEngine();
        }
        catch (Exception ex)
        {
            SetStatus(ExceptionText.ForUser(ex), error: true);
        }
        finally
        {
            DownloadButton.IsEnabled = true;
        }
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _modalDepth++;
        _settingsWindow = new SettingsWindow(_settings)
        {
            Owner = IsVisible ? this : null
        };
        try
        {
            var ok = _settingsWindow.ShowDialog() == true;
            if (ok && _settingsWindow.Result is not null)
            {
                SettingsStore.Save(_settingsWindow.Result);
                ApplySettings(_settingsWindow.Result, snapDock: true);
            }
            else
            {
                // Cancel already reverted theme in SettingsWindow; refresh local brushes.
                ApplyThemePalette(ThemeService.Apply(_settings.ColorTheme));
            }
        }
        finally
        {
            _settingsWindow = null;
            _modalDepth--;
        }
    }

    private void OnTrayShow(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Expand();
    }

    private void OnTrayExit(object sender, RoutedEventArgs e)
    {
        _allowClose = true;
        Application.Current.Shutdown();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        if (_settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _allowClose = true;
        Application.Current.Shutdown();
    }

    protected override void OnClosed(EventArgs e)
    {
        _tick.Stop();
        _downloadCts?.Cancel();
        try
        {
            if (_recorder.IsRecording)
            {
                _recorder.Stop();
            }
        }
        catch
        {
            // shutting down
        }

        _vad?.Dispose();
        _recorder.Dispose();
        _transcriber.Dispose();
        TrayIcon.Dispose();
        base.OnClosed(e);
    }
}
