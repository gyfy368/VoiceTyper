using System.Windows;
using System.Windows.Threading;
using VoiceTyper.Services;

namespace VoiceTyper;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUiException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                CrashLog.Write("appdomain-unhandled", ex);
                ShowError(ExceptionText.ForUser(ex));
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLog.Write("unobserved-task", args.Exception);
            ShowError(ExceptionText.ForUser(args.Exception));
            args.SetObserved();
        };
    }

    private static void OnUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        CrashLog.Write("dispatcher-unhandled", e.Exception);
        ShowError(ExceptionText.ForUser(e.Exception));
        e.Handled = true;
    }

    private static void ShowError(string message)
    {
        if (CrashLog.SuppressModalDialogs)
        {
            return;
        }

        MessageBox.Show(message, "VoiceTyper", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
