using TidyFlow.Core.Presets;
using TidyFlow.Core.Safety;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TidyFlow.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly BatchExecutor _executor = new();
    private readonly PresetStore _presetStore = new();

    private bool _isDarkTheme = true;
    private bool _isStatusOpen;
    private string _statusTitle = "Ready";
    private string _statusMessage = string.Empty;
    private InfoBarSeverity _statusSeverity = InfoBarSeverity.Informational;
    private string _undoButtonText = "Undo Last";
    private bool _canUndo;

    public MainViewModel()
    {
        Organize = new OrganizeViewModel(_executor, _presetStore, ShowStatus, RefreshUndoState);
        Rename = new RenameViewModel(_executor, _presetStore, ShowStatus, RefreshUndoState);

        UndoLatestCommand = new RelayCommand(UndoLatest, () => CanUndo);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        DismissStatusCommand = new RelayCommand(() => IsStatusOpen = false);

        RefreshUndoState();
    }

    public OrganizeViewModel Organize { get; }

    public RenameViewModel Rename { get; }

    public RelayCommand UndoLatestCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand DismissStatusCommand { get; }

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

    public string ThemeButtonText => _isDarkTheme ? "Light Mode" : "Dark Mode";

    public void RefreshUndoState()
    {
        var latest = _executor.PeekLatestUndoTransaction();
        if (latest is null)
        {
            CanUndo = false;
            UndoButtonText = "Undo Last";
            return;
        }

        CanUndo = true;
        UndoButtonText = $"Undo ({latest.Changes.Count})";
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

    private void ShowStatus(string message, bool isError)
    {
        StatusTitle = isError ? "Action Required" : "Completed";
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
