using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using TidyFlow.App.Services;
using TidyFlow.Core.Organizing;
using TidyFlow.Core.Presets;
using TidyFlow.Core.Safety;
using TidyFlow.Core.Watching;

namespace TidyFlow.App.ViewModels;

public sealed class AutomationViewModel : ObservableObject, IDisposable
{
    private readonly FolderWatchService _watchService;
    private readonly PresetStore _presetStore;
    private readonly Action<string, bool> _notifyStatus;
    private readonly Action _refreshUndoState;
    private readonly Action<string, string>? _showTrayNotification;

    private string _watchFolderPath = string.Empty;
    private OrganizeMode _watchMode = OrganizeMode.ByCustomRulesFirstThenCategory;
    private bool _isWatching;
    private bool _minimizeToTrayOnClose = true;
    private bool _isExplorerMenuRegistered;

    public AutomationViewModel(
        BatchExecutor executor,
        PresetStore presetStore,
        Action<string, bool> notifyStatus,
        Action refreshUndoState,
        Action<string, string>? showTrayNotification = null)
    {
        _presetStore = presetStore;
        _notifyStatus = notifyStatus;
        _refreshUndoState = refreshUndoState;
        _showTrayNotification = showTrayNotification;

        _watchService = new FolderWatchService(executor);
        _watchService.BatchProcessed += OnWatchBatchProcessed;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(userProfile, "Downloads");
        _watchFolderPath = Directory.Exists(downloads) ? downloads : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        _isExplorerMenuRegistered = ExplorerContextMenuService.IsRegistered();

        ToggleWatchCommand = new RelayCommand(ToggleWatch);
        ToggleExplorerMenuCommand = new RelayCommand(ToggleExplorerMenu);
        ClearLogsCommand = new RelayCommand(() => ActivityLogs.Clear(), () => ActivityLogs.Count > 0);
    }

    public ObservableCollection<string> ActivityLogs { get; } = new();

    public IReadOnlyList<OrganizeMode> AvailableWatchModes { get; } = Enum.GetValues<OrganizeMode>();

    public RelayCommand ToggleWatchCommand { get; }
    public RelayCommand ToggleExplorerMenuCommand { get; }
    public RelayCommand ClearLogsCommand { get; }

    public string WatchFolderPath
    {
        get => _watchFolderPath;
        set => SetProperty(ref _watchFolderPath, value);
    }

    public OrganizeMode WatchMode
    {
        get => _watchMode;
        set => SetProperty(ref _watchMode, value);
    }

    public bool IsWatching
    {
        get => _isWatching;
        private set
        {
            if (SetProperty(ref _isWatching, value))
            {
                OnPropertyChanged(nameof(WatchButtonText));
                OnPropertyChanged(nameof(WatchStatusText));
            }
        }
    }

    public string WatchButtonText => IsWatching ? "Stop Watch Folder" : "Start Watch Folder";

    public string WatchStatusText => IsWatching
        ? $"Active — Monitoring '{WatchFolderPath}'"
        : "Stopped — Enable to auto-organize newly downloaded files";

    public bool MinimizeToTrayOnClose
    {
        get => _minimizeToTrayOnClose;
        set => SetProperty(ref _minimizeToTrayOnClose, value);
    }

    public bool IsExplorerMenuRegistered
    {
        get => _isExplorerMenuRegistered;
        private set
        {
            if (SetProperty(ref _isExplorerMenuRegistered, value))
            {
                OnPropertyChanged(nameof(ExplorerMenuButtonText));
                OnPropertyChanged(nameof(ExplorerMenuStatusText));
            }
        }
    }

    public string ExplorerMenuButtonText => IsExplorerMenuRegistered
        ? "Remove Explorer Context Menu"
        : "Add Explorer Right-Click Menu";

    public string ExplorerMenuStatusText => IsExplorerMenuRegistered
        ? "Registered (Right-click files/folders in Windows Explorer -> TidyFlow)"
        : "Not registered (Per-user HKCU registration, no admin rights needed)";

    public event EventHandler<bool>? WatchStateChanged;

    public void ToggleWatch()
    {
        if (IsWatching)
        {
            _watchService.Stop();
            IsWatching = false;
            ActivityLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Stopped watching {WatchFolderPath}");
            WatchStateChanged?.Invoke(this, false);
            return;
        }

        if (string.IsNullOrWhiteSpace(WatchFolderPath) || !Directory.Exists(WatchFolderPath))
        {
            _notifyStatus("Watch folder does not exist.", true);
            return;
        }

        var presets = _presetStore.Load();
        _watchService.Start(WatchFolderPath, new OrganizeOptions
        {
            Mode = WatchMode,
            CustomRules = presets.CustomOrganizeRules
        });

        IsWatching = true;
        ActivityLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Watching {WatchFolderPath} ({WatchMode})");
        WatchStateChanged?.Invoke(this, true);
        _notifyStatus($"Watch Folder started for '{Path.GetFileName(WatchFolderPath)}'.", false);
    }

    private void ToggleExplorerMenu()
    {
        try
        {
            if (IsExplorerMenuRegistered)
            {
                ExplorerContextMenuService.Unregister();
                IsExplorerMenuRegistered = false;
                _notifyStatus("Removed TidyFlow from Windows Explorer right-click menu.", false);
            }
            else
            {
                ExplorerContextMenuService.Register();
                IsExplorerMenuRegistered = true;
                _notifyStatus("Added TidyFlow to Windows Explorer right-click menu!", false);
            }
        }
        catch (Exception ex)
        {
            _notifyStatus($"Explorer menu update failed: {ex.Message}", true);
        }
    }

    private void OnWatchBatchProcessed(object? sender, WatchOrganizeEventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _refreshUndoState();
            foreach (var desc in e.MovedDescriptions)
            {
                ActivityLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {desc}");
            }

            if (!string.IsNullOrWhiteSpace(e.ErrorMessage))
            {
                ActivityLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Error: {e.ErrorMessage}");
            }
            else if (e.MovedCount > 0)
            {
                _showTrayNotification?.Invoke("TidyFlow Watch Folder", $"Organized {e.MovedCount} new file(s).");
            }

            RelayCommand.RaiseCanExecuteChanged();
        });
    }

    public void Dispose() => _watchService.Dispose();
}
