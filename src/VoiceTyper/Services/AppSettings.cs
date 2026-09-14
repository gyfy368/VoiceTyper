using System.Text.Json.Serialization;

namespace VoiceTyper.Services;

public sealed class AppSettings
{
    /// <summary>Exit = 关闭即退出；Tray = 关闭后仅藏到托盘。</summary>
    public string CloseBehavior { get; set; } = CloseBehaviors.Exit;

    public bool ShowInTaskbar { get; set; } = true;

    public bool Topmost { get; set; } = true;

    public bool CollapseOnDeactivate { get; set; }

    public bool VadAutoStop { get; set; } = true;

    public bool AutoCopy { get; set; } = true;

    /// <summary>转写完成（含自动复制）后是否收成胶囊。默认关，方便改字。</summary>
    public bool CollapseAfterDone { get; set; }

    /// <summary>Left / Right</summary>
    public string DockSide { get; set; } = DockSides.Right;

    /// <summary>Kept for old settings.json; Normalize always forces Off.</summary>
    public string ShadowMode { get; set; } = ShadowModes.Off;

    /// <summary>Kept for old settings.json; Normalize always forces true.</summary>
    public bool AnimationsEnabled { get; set; } = true;

    /// <summary>InkBlack / WarmPaper / CoolGray / Forest</summary>
    public string ColorTheme { get; set; } = ColorThemes.InkBlack;

    /// <summary>Small / Medium / Large / Custom</summary>
    public string PaperSizePreset { get; set; } = PaperSize.Presets.Medium;

    public double PaperWidth { get; set; } = PaperSize.DefaultWidth;

    public double PaperHeight { get; set; } = PaperSize.DefaultHeight;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PaperLeft { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PaperTop { get; set; }

    [JsonIgnore]
    public bool CloseToTray =>
        string.Equals(CloseBehavior, CloseBehaviors.Tray, StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool DockRight =>
        !string.Equals(DockSide, DockSides.Left, StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool ShadowLight => false;

    [JsonIgnore]
    public bool HasPaperPosition => PaperLeft.HasValue && PaperTop.HasValue;

    public AppSettings Clone() => new()
    {
        CloseBehavior = CloseBehavior,
        ShowInTaskbar = ShowInTaskbar,
        Topmost = Topmost,
        CollapseOnDeactivate = CollapseOnDeactivate,
        VadAutoStop = VadAutoStop,
        AutoCopy = AutoCopy,
        CollapseAfterDone = CollapseAfterDone,
        DockSide = DockSide,
        ShadowMode = ShadowMode,
        AnimationsEnabled = AnimationsEnabled,
        ColorTheme = ColorTheme,
        PaperSizePreset = PaperSizePreset,
        PaperWidth = PaperWidth,
        PaperHeight = PaperHeight,
        PaperLeft = PaperLeft,
        PaperTop = PaperTop
    };
}

public static class CloseBehaviors
{
    public const string Exit = "Exit";
    public const string Tray = "Tray";
}

public static class DockSides
{
    public const string Left = "Left";
    public const string Right = "Right";
}

public static class ShadowModes
{
    public const string Off = "Off";
    public const string Light = "Light";
}

public static class ColorThemes
{
    public const string InkBlack = "InkBlack";
    public const string WarmPaper = "WarmPaper";
    public const string CoolGray = "CoolGray";
    public const string Forest = "Forest";
}
