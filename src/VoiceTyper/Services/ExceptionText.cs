using System.Runtime.InteropServices;

namespace VoiceTyper.Services;

/// <summary>
/// Turns raw CLR exceptions into short Chinese UI text.
/// Never show English NullReferenceException wording to the user.
/// </summary>
public static class ExceptionText
{
    public static string ForUser(Exception? ex)
    {
        if (ex is null)
        {
            return "出了点问题，请再试一次。";
        }

        if (ex is NullReferenceException || IsEnglishNullReferenceMessage(ex.Message))
        {
            return "出了点问题，请再点一次麦克风重试。若反复出现，请重启 VoiceTyper。";
        }

        if (ex is SEHException ||
            ex.Message.Contains("External component", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("vad is nullptr", StringComparison.OrdinalIgnoreCase))
        {
            return "停录时语音检测异常，请再试一次。";
        }

        var message = ex.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return "出了点问题，请再试一次。";
        }

        return message;
    }

    private static bool IsEnglishNullReferenceMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("Object reference not set", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Object reference", StringComparison.OrdinalIgnoreCase);
    }
}
