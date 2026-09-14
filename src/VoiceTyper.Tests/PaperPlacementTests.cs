using System.Windows;
using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class PaperPlacementTests
{
    private static readonly Rect Work = new(0, 0, 1920, 1080);

    [Fact]
    public void Expanded_paper_does_not_snap_after_drag()
    {
        Assert.False(PaperPlacement.SnapAfterDrag(expanded: true));
        Assert.True(PaperPlacement.SnapAfterDrag(expanded: false));
    }

    [Fact]
    public void Capsule_docks_to_chosen_edge()
    {
        var right = PaperPlacement.Dock(dockRight: true, width: 188, height: 56, Work, currentTop: 400);
        Assert.Equal(1920 - 188 - 10, right.Left);
        Assert.Equal(400, right.Top);

        var left = PaperPlacement.Dock(dockRight: false, width: 188, height: 56, Work, currentTop: 400);
        Assert.Equal(10, left.Left);
    }

    [Fact]
    public void Free_drag_keeps_user_position_without_snapping_to_edge()
    {
        var pos = PaperPlacement.ClampFree(640, 220, 320, 428, Work);
        Assert.Equal(640, pos.Left);
        Assert.Equal(220, pos.Top);
    }

    [Fact]
    public void Expand_uses_saved_position_when_present()
    {
        var pos = PaperPlacement.ExpandTarget(
            hasSaved: true,
            savedLeft: 500,
            savedTop: 180,
            dockRight: true,
            width: 320,
            height: 428,
            work: Work,
            currentTop: 400);
        Assert.Equal(500, pos.Left);
        Assert.Equal(180, pos.Top);
    }

    [Fact]
    public void Expand_without_saved_position_grows_from_dock()
    {
        var pos = PaperPlacement.ExpandTarget(
            hasSaved: false,
            savedLeft: null,
            savedTop: null,
            dockRight: true,
            width: 320,
            height: 428,
            work: Work,
            currentTop: 400);
        Assert.Equal(1920 - 320 - 10, pos.Left);
        Assert.Equal(400, pos.Top);
    }

    [Fact]
    public void Persist_when_settings_already_had_position_or_after_first_drag()
    {
        Assert.True(PaperPlacement.ShouldPersist(settingsHadPosition: true, userDragged: false));
        Assert.True(PaperPlacement.ShouldPersist(settingsHadPosition: false, userDragged: true));
        Assert.False(PaperPlacement.ShouldPersist(settingsHadPosition: false, userDragged: false));
    }
}
