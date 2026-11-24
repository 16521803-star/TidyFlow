using TidyFlow.App.Services;
using TidyFlow.Core.Presets;
using TidyFlow.Core.Safety;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TidyFlow.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly BatchExecutor _executor = new();
    private readonly PresetStore _presetStore = new();
    private readonly AppUpdateService _updateService = new();

    private bool _isDarkTheme = true;
    private int _activeTabIndex;
    private bool _isStatusOpen;
    private string _statusTitle = "Ready";
    private string _statusMessage = string.Empty;
    private InfoBarSeverity _statusSeverity = InfoBarSeverity.Informational;
    private string _undoButtonText = "Undo Last";
    private bool _canUndo;
    private bool _isCheckingUpdates;
    private LanguageOption _selectedLanguage;

    public MainViewModel(Action<string, string>? showTrayNotification = null)
    {
        Organize = new OrganizeViewModel(_executor, _presetStore, ShowStatus, RefreshUndoState);
        Rename = new RenameViewModel(_executor, _presetStore, ShowStatus, RefreshUndoState);
        Duplicates = new DuplicatesViewModel(ShowStatus);
        SpaceCleaner = new SpaceCleanerViewModel(ShowStatus);
        ImageResizer = new ImageResizerViewModel(ShowStatus);
        Automation = new AutomationViewModel(_executor, _presetStore, ShowStatus, RefreshUndoState, showTrayNotification);
        SmartAi = new SmartAiViewModel(_executor, ShowStatus, RefreshUndoState);

        UndoLatestCommand = new RelayCommand(UndoLatest, () => CanUndo);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        CheckUpdatesCommand = new RelayCommand(CheckUpdatesAsync, () => !IsCheckingUpdates);
        DismissStatusCommand = new RelayCommand(() => IsStatusOpen = false);

        _selectedLanguage = AvailableLanguages.FirstOrDefault(l =>
                                string.Equals(l.Code, LocalizationService.Instance.CurrentLanguageCode, StringComparison.OrdinalIgnoreCase))
                            ?? AvailableLanguages[0];

        LocalizationService.Instance.LanguageChanged += (_, _) => OnLanguageChanged();

        RefreshUndoState();
    }

    public OrganizeViewModel Organize { get; }

    public RenameViewModel Rename { get; }

    public DuplicatesViewModel Duplicates { get; }

    public SpaceCleanerViewModel SpaceCleaner { get; }

    public ImageResizerViewModel ImageResizer { get; }

    public AutomationViewModel Automation { get; }

    public SmartAiViewModel SmartAi { get; }

    public IReadOnlyList<LanguageOption> AvailableLanguages => LocalizationService.Instance.AvailableLanguages;

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is not null && SetProperty(ref _selectedLanguage, value))
            {
                LocalizationService.Instance.SetLanguage(value.Code);
            }
        }
    }

    public RelayCommand UndoLatestCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand CheckUpdatesCommand { get; }
    public RelayCommand DismissStatusCommand { get; }

    public string AppVersionText => _updateService.CurrentVersionDisplay;

    public int ActiveTabIndex
    {
        get => _activeTabIndex;
        set => SetProperty(ref _activeTabIndex, value);
    }

    public bool IsCheckingUpdates
    {
        get => _isCheckingUpdates;
        private set
        {
            if (SetProperty(ref _isCheckingUpdates, value))
            {
                OnPropertyChanged(nameof(UpdateButtonText));
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string UpdateButtonText => IsCheckingUpdates
        ? LocalizationService.Instance.Get("Str_CheckingUpdates", "Checking...")
        : LocalizationService.Instance.Format("Str_UpdatesFmt", AppVersionText);

    public bool IsStatusOpen
    {
        get => _isStatusOpen;
        set => SetProperty(ref _isStatusOpen, value);
    }

    public string StatusTitle
    {
        get => _statusTitle;
        private set => SetProperty(ref _statusTitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public InfoBarSeverity StatusSeverity
    {
        get => _statusSeverity;
        private set => SetProperty(ref _statusSeverity, value);
    }

    public string UndoButtonText
    {
        get => _undoButtonText;
        private set => SetProperty(ref _undoButtonText, value);
    }

    public bool CanUndo
    {
        get => _canUndo;
        private set
        {
            if (SetProperty(ref _canUndo, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ThemeButtonText => _isDarkTheme
        ? LocalizationService.Instance.Get("Str_LightMode", "Light Mode")
        : LocalizationService.Instance.Get("Str_DarkMode", "Dark Mode");

    public void ApplyStartupArgs(string[] args)
    {
        if (args.Length < 2)
        {
            return;
        }

        if (string.Equals(args[0], "--organize", StringComparison.OrdinalIgnoreCase))
        {
            var folder = args[1];
            if (System.IO.Directory.Exists(folder))
            {
                ActiveTabIndex = 0;
                Organize.FolderPath = folder;
                Organize.ScanAndPreview();
            }
        }
        else if (string.Equals(args[0], "--rename", StringComparison.OrdinalIgnoreCase))
        {
            var paths = args.Skip(1).Where(p => System.IO.File.Exists(p) || System.IO.Directory.Exists(p)).ToArray();
            if (paths.Length > 0)
            {
                ActiveTabIndex = 1;
                Rename.AddPaths(paths);
            }
        }
    }

    public void RefreshUndoState()
    {
        var latest = _executor.PeekLatestUndoTransaction();
        if (latest is null)
        {
            CanUndo = false;
            UndoButtonText = LocalizationService.Instance.Get("Str_UndoLast", "Undo Last");
            return;
        }

        CanUndo = true;
        UndoButtonText = LocalizationService.Instance.Format("Str_UndoCount", latest.Changes.Count);
    }

    private void OnLanguageChanged()
    {
        RefreshUndoState();
        OnPropertyChanged(nameof(UpdateButtonText));
        OnPropertyChanged(nameof(ThemeButtonText));
        Organize.RefreshLocalization();
        Rename.RefreshLocalization();
        ImageResizer.RefreshLocalization();
        Automation.RefreshLocalization();
    }

    private void UndoLatest()
    {
        var result = _executor.UndoLatest();
        RefreshUndoState();

        if (result.FailedCount > 0)
        {
            ShowStatus($"Undo partially failed ({result.RestoredCount} restored): {string.Join("; ", result.Errors)}", isError: true);
        }
        else
        {
            ShowStatus($"Undid last batch: restored {result.RestoredCount} file(s).", isError: false);
        }

        Organize.ScanAndPreview();
    }

    private async void CheckUpdatesAsync()
    {
        if (IsCheckingUpdates)
        {
            return;
        }

        IsCheckingUpdates = true;
        try
        {
            var outcome = await _updateService.CheckAndDownloadAsync();
            StatusTitle = outcome.UpdateAvailable ? "Update Ready" : "Updates";
            StatusMessage = outcome.Message;
            StatusSeverity = outcome.UpdateAvailable ? InfoBarSeverity.Success : InfoBarSeverity.Informational;
            IsStatusOpen = true;
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusTitle = isError
            ? LocalizationService.Instance.Get("Str_StatusError", "Action Required")
            : LocalizationService.Instance.Get("Str_StatusCompleted", "Completed");
        StatusMessage = message;
        StatusSeverity = isError ? InfoBarSeverity.Error : InfoBarSeverity.Success;
        IsStatusOpen = true;
    }

    private void ToggleTheme()
    {
        _isDarkTheme = !_isDarkTheme;
        ApplicationThemeManager.Apply(_isDarkTheme ? ApplicationTheme.Dark : ApplicationTheme.Light);
        OnPropertyChanged(nameof(ThemeButtonText));
    }
}
