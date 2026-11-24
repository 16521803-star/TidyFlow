using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace TidyFlow.App.Services;

public sealed record UpdateCheckOutcome(bool UpdateAvailable, bool AppliedAndRestarting, string Message);

public sealed class AppUpdateService
{
    private const string RepoUrl = "https://github.com/16521803-star/TidyFlow";

    public string CurrentVersionDisplay
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "v0.7.0" : $"v{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public async Task<UpdateCheckOutcome> CheckAndDownloadAsync()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
            if (!manager.IsInstalled)
            {
                return new UpdateCheckOutcome(
                    false,
                    false,
                    $"Running portable/dev build ({CurrentVersionDisplay}). Auto-update is active when installed via the GitHub Release setup.");
            }

            var checkTask = manager.CheckForUpdatesAsync();
            var completed = await Task.WhenAny(checkTask, Task.Delay(TimeSpan.FromSeconds(15)));
            if (completed != checkTask)
            {
                return new UpdateCheckOutcome(false, false, "Update check timed out. Check your internet connection or GitHub Releases.");
            }

            var updateInfo = await checkTask;
            if (updateInfo is null)
            {
                return new UpdateCheckOutcome(false, false, $"You are on the latest version ({CurrentVersionDisplay}).");
            }

            await manager.DownloadUpdatesAsync(updateInfo);
            manager.ApplyUpdatesAndRestart(updateInfo);

            return new UpdateCheckOutcome(
                true,
                true,
                $"Downloaded {updateInfo.TargetFullRelease.Version}. Restarting to apply update...");
        }
        catch (Exception ex)
        {
            return new UpdateCheckOutcome(false, false, $"Update check skipped: {ex.Message}");
        }
    }
}
