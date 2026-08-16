using System.Windows;
using System.Windows.Threading;
using System;
using System.IO;

namespace AiTaskTracker;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        base.OnStartup(e);
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        TryWriteCrashLog(e.Exception);
        try
        {
            var owner = Current?.MainWindow;
            var errorWindow = new ErrorReportWindow(owner, e.Exception);
            errorWindow.ShowDialog();
        }
        catch
        {
            MessageBox.Show(e.Exception.Message, "AI Task Tracker error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        e.Handled = true;
    }

    private static void TryWriteCrashLog(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AiTaskTracker",
                "CrashReports");
            Directory.CreateDirectory(directory);
            var fileName = $"crash-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log";
            File.WriteAllText(Path.Combine(directory, fileName), exception.ToString());
        }
        catch
        {
            // Avoid recursive dispatcher failures while reporting the original crash.
        }
    }
}
