using System.Text.RegularExpressions;

namespace VoiceTyper.Services;

/// <summary>
/// SenseVoice ITN often glues Chinese and ASCII punctuation, especially a trailing "。,".
/// </summary>
public static class PunctuationCleanup
{
    private static readonly Regex CjkThenAscii = new(@"[。．、，！？；：][,.]+", RegexOptions.Compiled);
    private static readonly Regex AsciiThenCjk = new(@"[,.]+[。．、，！？；：]", RegexOptions.Compiled);
    private static readonly Regex RepeatedCjkStop = new(@"[。．]{2,}", RegexOptions.Compiled);
    private static readonly Regex RepeatedComma = new(@"[，,]{2,}", RegexOptions.Compiled);
    private static readonly Regex TrailingMixed = new(@"[。．.，,、；;：:！!？?]+$", RegexOptions.Compiled);

    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var t = text.Trim();
        t = CjkThenAscii.Replace(t, m => m.Value[0].ToString());
        t = AsciiThenCjk.Replace(t, m => m.Value[^1].ToString());
        t = RepeatedCjkStop.Replace(t, "。");
        t = RepeatedComma.Replace(t, m => m.Value.Contains('，') ? "，" : ",");
        t = CollapseTrailing(t);
        return t.Trim();
    }

    private static string CollapseTrailing(string text)
    {
        var match = TrailingMixed.Match(text);
        if (!match.Success || match.Length <= 1)
        {
            return text;
        }

        var cluster = match.Value;
        var keep = PreferTrailing(cluster);
        return text[..match.Index] + keep;
    }

    private static char PreferTrailing(string cluster)
    {
        if (cluster.IndexOfAny(['？', '?']) >= 0)
        {
            return cluster.Contains('？') ? '？' : '?';
        }

        if (cluster.IndexOfAny(['！', '!']) >= 0)
        {
            return cluster.Contains('！') ? '！' : '!';
        }

        if (cluster.IndexOfAny(['。', '．', '.']) >= 0)
        {
            return cluster.Contains('。') || cluster.Contains('．') ? '。' : '.';
        }

        if (cluster.Contains('，'))
        {
            return '，';
        }

        return cluster[^1];
    }
}
