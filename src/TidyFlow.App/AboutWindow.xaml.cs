using System.Diagnostics;
using System.IO;
using System.Windows;
using Wpf.Ui.Controls;

namespace TidyFlow.App;

public partial class AboutWindow : FluentWindow
{
    public AboutWindow(string versionText)
    {
        InitializeComponent();
        VersionTextBlock.Text = versionText;
    }

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
        => OpenUrl("https://github.com/16521803-star/TidyFlow");

    private void OpenReleases_Click(object sender, RoutedEventArgs e)
        => OpenUrl("https://github.com/16521803-star/TidyFlow/releases");

    private void OpenHistory_Click(object sender, RoutedEventArgs e)
    {
        var historyDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TidyFlow",
            "history");
        Directory.CreateDirectory(historyDir);
        Process.Start(new ProcessStartInfo
        {
            FileName = historyDir,
            UseShellExecute = true
        });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
