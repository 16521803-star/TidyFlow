using System.IO;
using System.Windows;
using System.Windows.Threading;
using TidyFlow.App.Services;

namespace TidyFlow.App;

public partial class App : Application
{
    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            LocalizationService.Instance.Initialize();
            var window = new MainWindow
            {
                WindowState = WindowState.Normal,
                ShowInTaskbar = true
            };
            MainWindow = window;
            window.ApplyStartupArgs(Program.StartupArgs);
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            LogException("Startup", ex);
            MessageBox.Show(
                $"TidyFlow failed to start:\n\n{ex.Message}",
                "TidyFlow Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("Dispatcher", e.Exception);
        MessageBox.Show(
            $"Unexpected UI error:\n\n{e.Exception.Message}",
            "TidyFlow Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException("AppDomain", ex);
        }
    }

    private static void LogException(string source, Exception ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TidyFlow");
            Directory.CreateDirectory(dir);
            var logFile = Path.Combine(dir, "app_errors.log");
            File.AppendAllText(logFile, $"[{DateTimeOffset.Now:O}] [{source}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging failures.
        }
    }
}
