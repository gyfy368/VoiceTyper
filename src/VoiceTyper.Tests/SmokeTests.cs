using VoiceTyper.Services;
using Xunit;

namespace VoiceTyper.Tests;

public class SmokeTests
{
    [Fact]
    public void Core_types_can_be_imported()
    {
        Assert.Equal("Recorder", typeof(Recorder).Name);
        Assert.Equal("Transcriber", typeof(Transcriber).Name);
        Assert.Equal("ClipboardService", typeof(ClipboardService).Name);
        Assert.Equal("SessionController", typeof(SessionController).Name);
        Assert.Equal("VadMonitor", typeof(VadMonitor).Name);
        Assert.Equal("ModelLocator", typeof(ModelLocator).Name);
        Assert.Equal(16000, Recorder.SampleRate);
    }
}
