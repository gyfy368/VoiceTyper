using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using VoiceTyper.Services;

namespace VoiceTyper;

public partial class MainWindow : Window
{
    private const double CapsuleWidth = 188;
    private const double CapsuleHeight = 56;
    /// <summary>Collapse/expand: current surface fades out, geometry swaps, next surface fades in.</summary>
    private const int SurfaceFadeOutMs = 280;
    private const int SurfaceFadeInMs = 340;
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
    private int _continueCaret;
    private string? _undoContinueText;
    private int _undoContinueCaret;
    private bool _hasContinueUndo;
    private bool _applyingTranscript;
    private DispatcherTimer? _highlightTimer;
    private DispatcherTimer? _toastTimer;
    private Storyboard? _pulseStory;
    private Storyboard? _scanStory;

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
        Opacity = _settings.WindowOpacity;
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
        _animating = true;
        Shell.IsHitTestVisible = false;

        var fadeOut = new DoubleAnimation(Shell.Opacity, 0, TimeSpan.FromMilliseconds(SurfaceFadeOutMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            Shell.BeginAnimation(OpacityProperty, null);
            Shell.Opacity = 0;
            CommitSurface(width, height, paper);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(SurfaceFadeInMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var scaleIn = new DoubleAnimation(0.985, 1, TimeSpan.FromMilliseconds(SurfaceFadeInMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            fadeIn.Completed += (_, _) =>
            {
                Shell.BeginAnimation(OpacityProperty, null);
                ShellScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                ShellScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                Shell.Opacity = 1;
                ShellScale.ScaleX = 1;
                ShellScale.ScaleY = 1;
                Shell.IsHitTestVisible = true;
                _animating = false;
                _suppressSizePersist = false;
            };
            ShellScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
            ShellScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn.Clone());
            Shell.BeginAnimation(OpacityProperty, fadeIn);
        };
        Shell.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void CommitSurface(double width, double height, bool paper)
    {
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        PaperLayer.BeginAnimation(OpacityProperty, null);
        CapsuleLayer.BeginAnimation(OpacityProperty, null);

        if (paper)
        {
            MinWidth = PaperSize.MinWidth;
            MinHeight = PaperSize.MinHeight;
            MaxWidth = PaperSize.MaxWidth;
            MaxHeight = PaperSize.MaxHeight;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Width = width;
            Height = height;
            ApplyExpandedVisuals();
        }
        else
        {
            ResizeMode = ResizeMode.NoResize;
            ApplyCollapsedVisuals(animate: false);
        }
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
        if (slot == MicSlot.Continue)
        {
            _continueCaret = TranscriptBox.CaretIndex;
        }
        else
        {
            ClearContinueUndo();
        }

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
            var slot = _recordSlot;
            var append = MicChrome.AppendNext(slot);
            var existing = TranscriptBox.Text;
            var join = TranscriptJoin.ApplyAt(
                existing,
                text,
                append,
                append ? _continueCaret : -1);
            _applyingTranscript = true;
            try
            {
                TranscriptBox.Text = join.Text;
            }
            finally
            {
                _applyingTranscript = false;
            }

            if (join.InsertLength > 0)
            {
                TranscriptBox.CaretIndex = join.InsertStart + join.InsertLength;
            }

            if (append && join.InsertLength > 0)
            {
                _undoContinueText = existing;
                _undoContinueCaret = _continueCaret;
                _hasContinueUndo = true;
                if (UndoContinueButton is not null)
                {
                    UndoContinueButton.Visibility = Visibility.Visible;
                }

                HighlightInserted(join.InsertStart, join.InsertLength);
            }
            else
            {
                ClearContinueUndo();
                PlayTextEntrance();
            }

            _recordSlot = MicSlot.Single;
            SyncMicLayout(animate: true);

            var copied = false;
            if (_settings.AutoCopy)
            {
                var payload = CopyPayload.Resolve(join.Text, join.Segment, append, _settings.CopyMode);
                if (!_clipboard.TryCopy(payload, out var message))
                {
                    SetStatus(message);
                    CapsuleLabel.Text = "没听清";
                    CapsuleDot.Fill = _brass;
                    return;
                }

                copied = true;
                ShowCopyToast();
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

    private void SetRecordingVisual(bool on)
    {
        try
        {
            StopPulse();
            HideAllPulses();
            if (on)
            {
                FillActiveHalo(_ember);
                var (ring, scale) = ActivePulse();
                StartPulse(ring, scale);
            }
            else
            {
                FillIdleHalos();
            }
        }
        catch
        {
            HideAllPulses();
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
            StopScan();
            HideAllScans();
            if (on)
            {
                var (arc, rotate) = ActiveScan();
                StartScan(arc, rotate);
            }
        }
        catch
        {
            HideAllScans();
        }
    }

    private (Ellipse Ring, ScaleTransform Scale) ActivePulse()
    {
        if (_dualMic && _recordSlot == MicSlot.NewSentence && PulseRingNew is not null)
        {
            return (PulseRingNew, PulseScaleNew);
        }

        if (_dualMic && _recordSlot == MicSlot.Continue && PulseRingContinue is not null)
        {
            return (PulseRingContinue, PulseScaleContinue);
        }

        return (PulseRing, PulseScale);
    }

    private (System.Windows.Shapes.Path Arc, RotateTransform Rotate) ActiveScan()
    {
        if (_dualMic && _recordSlot == MicSlot.NewSentence && ScanArcNew is not null)
        {
            return (ScanArcNew, ScanRotateNew);
        }

        if (_dualMic && _recordSlot == MicSlot.Continue && ScanArcContinue is not null)
        {
            return (ScanArcContinue, ScanRotateContinue);
        }

        return (ScanArc, ScanRotate);
    }

    private void HideAllPulses()
    {
        ResetPulse(PulseRing, PulseScale);
        ResetPulse(PulseRingNew, PulseScaleNew);
        ResetPulse(PulseRingContinue, PulseScaleContinue);
    }

    private static void ResetPulse(Ellipse? ring, ScaleTransform? scale)
    {
        if (ring is not null)
        {
            ring.BeginAnimation(OpacityProperty, null);
            ring.Opacity = 0;
        }

        if (scale is not null)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }
    }

    private void HideAllScans()
    {
        ResetScan(ScanArc, ScanRotate);
        ResetScan(ScanArcNew, ScanRotateNew);
        ResetScan(ScanArcContinue, ScanRotateContinue);
    }

    private static void ResetScan(System.Windows.Shapes.Path? arc, RotateTransform? rotate)
    {
        if (arc is not null)
        {
            arc.BeginAnimation(OpacityProperty, null);
            arc.Opacity = 0;
        }

        if (rotate is not null)
        {
            rotate.BeginAnimation(RotateTransform.AngleProperty, null);
            rotate.Angle = 0;
        }
    }

    private void StartPulse(Ellipse ring, ScaleTransform scale)
    {
        StopPulse();
        ring.Opacity = 0.28;
        var ease = new QuinticEase { EasingMode = EasingMode.EaseInOut };
        var duration = TimeSpan.FromSeconds(1.85);
        var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        var opacity = new DoubleAnimation(0.28, 0.06, duration) { AutoReverse = true, EasingFunction = ease };
        Storyboard.SetTarget(opacity, ring);
        Storyboard.SetTargetProperty(opacity, new PropertyPath(OpacityProperty));
        var scaleX = new DoubleAnimation(1.0, 1.06, duration) { AutoReverse = true, EasingFunction = ease };
        Storyboard.SetTarget(scaleX, scale);
        Storyboard.SetTargetProperty(scaleX, new PropertyPath(ScaleTransform.ScaleXProperty));
        var scaleY = new DoubleAnimation(1.0, 1.06, duration) { AutoReverse = true, EasingFunction = ease };
        Storyboard.SetTarget(scaleY, scale);
        Storyboard.SetTargetProperty(scaleY, new PropertyPath(ScaleTransform.ScaleYProperty));
        sb.Children.Add(opacity);
        sb.Children.Add(scaleX);
        sb.Children.Add(scaleY);
        sb.Begin();
        _pulseStory = sb;
    }

    private void StopPulse()
    {
        _pulseStory?.Stop();
        _pulseStory = null;
    }

    private void StartScan(System.Windows.Shapes.Path arc, RotateTransform rotate)
    {
        StopScan();
        arc.Opacity = 1;
        var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.65));
        Storyboard.SetTarget(spin, rotate);
        Storyboard.SetTargetProperty(spin, new PropertyPath(RotateTransform.AngleProperty));
        sb.Children.Add(spin);
        sb.Begin();
        _scanStory = sb;
    }

    private void StopScan()
    {
        _scanStory?.Stop();
        _scanStory = null;
    }

    private void PlayTextEntrance()
    {
        TranscriptBox.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private void HighlightInserted(int start, int length)
    {
        CancelHighlight();
        if (length <= 0 || start < 0 || start + length > TranscriptBox.Text.Length)
        {
            return;
        }

        TranscriptBox.SelectionBrush = _ember;
        TranscriptBox.SelectionOpacity = 0.55;
        TranscriptBox.Select(start, length);
        _highlightTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2200) };
        _highlightTimer.Tick += (_, _) =>
        {
            CancelHighlight();
            TranscriptBox.CaretIndex = Math.Min(start + length, TranscriptBox.Text.Length);
        };
        _highlightTimer.Start();
    }

    private void CancelHighlight()
    {
        _highlightTimer?.Stop();
        _highlightTimer = null;
        try
        {
            TranscriptBox.SelectionBrush = FindResource("BrassBrush") as Brush ?? _brass;
            TranscriptBox.SelectionOpacity = 0.35;
            var caret = TranscriptBox.CaretIndex;
            TranscriptBox.SelectionLength = 0;
            TranscriptBox.CaretIndex = caret;
        }
        catch
        {
            // TextBox may not be ready during shutdown.
        }
    }

    private void ShowCopyToast()
    {
        if (!_settings.ShowCopyToast || CopyToast is null)
        {
            return;
        }

        _toastTimer?.Stop();
        CopyToast.Visibility = Visibility.Visible;
        CopyToast.BeginAnimation(OpacityProperty, null);
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        fadeIn.Completed += (_, _) =>
        {
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
            _toastTimer.Tick += (_, _) =>
            {
                _toastTimer.Stop();
                var fadeOut = new DoubleAnimation(CopyToast.Opacity, 0, TimeSpan.FromMilliseconds(260))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                fadeOut.Completed += (_, _) =>
                {
                    CopyToast.BeginAnimation(OpacityProperty, null);
                    CopyToast.Opacity = 0;
                    CopyToast.Visibility = Visibility.Collapsed;
                };
                CopyToast.BeginAnimation(OpacityProperty, fadeOut);
            };
            _toastTimer.Start();
        };
        CopyToast.BeginAnimation(OpacityProperty, fadeIn);
    }

    private void ClearContinueUndo()
    {
        _hasContinueUndo = false;
        _undoContinueText = null;
        if (UndoContinueButton is not null)
        {
            UndoContinueButton.Visibility = Visibility.Collapsed;
        }
    }

    private void OnUndoContinue(object sender, RoutedEventArgs e)
    {
        if (!_hasContinueUndo || _undoContinueText is null)
        {
            return;
        }

        CancelHighlight();
        _applyingTranscript = true;
        try
        {
            TranscriptBox.Text = _undoContinueText;
            TranscriptBox.CaretIndex = Math.Clamp(_undoContinueCaret, 0, TranscriptBox.Text.Length);
        }
        finally
        {
            _applyingTranscript = false;
        }

        ClearContinueUndo();
        SetStatus("已撤回刚才继续说的内容");
    }

    private void OnTranscriptPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control && _hasContinueUndo)
        {
            OnUndoContinue(sender, e);
            e.Handled = true;
        }
    }

    private void OnTranscriptChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        PlaceholderText.Visibility = string.IsNullOrWhiteSpace(TranscriptBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (!_applyingTranscript)
        {
            CancelHighlight();
        }

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
        if (_session.Phase != AppPhase.Recording)
        {
            return;
        }

        StopPulse();
        HideAllPulses();
        var (ring, scale) = ActivePulse();
        StartPulse(ring, scale);
    }

    private void PlaceScanOnActiveSlot()
    {
        if (_session.Phase != AppPhase.Transcribing)
        {
            return;
        }

        StopScan();
        HideAllScans();
        var (arc, rotate) = ActiveScan();
        StartScan(arc, rotate);
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
        _settingsWindow.WindowOpacityPreview += value => Opacity = value;
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
                Opacity = _settings.WindowOpacity;
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
