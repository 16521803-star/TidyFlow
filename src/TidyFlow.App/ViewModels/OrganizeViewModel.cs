using System.Collections.ObjectModel;
using System.IO;
using TidyFlow.App.Models;
using TidyFlow.Core.Models;
using TidyFlow.Core.Organizing;
using TidyFlow.Core.Presets;
using TidyFlow.Core.Safety;
using TidyFlow.Core.Scanning;

namespace TidyFlow.App.ViewModels;

public sealed record CustomRuleItem(
    string TargetFolder,
    string KeywordsCsv,
    string ExtensionsCsv)
{
    public KeywordOrganizeRule ToCoreRule()
    {
        var keywords = KeywordsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var extensions = ExtensionsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new KeywordOrganizeRule(TargetFolder.Trim(), keywords, extensions);
    }

    public static CustomRuleItem FromCoreRule(KeywordOrganizeRule rule) =>
        new(
            rule.TargetFolderName,
            string.Join(", ", rule.Keywords),
            string.Join(", ", rule.Extensions));
}

public sealed class OrganizeViewModel : ObservableObject
{
    private readonly FolderScanner _scanner = new();
    private readonly FolderOrganizer _organizer = new();
    private readonly BatchExecutor _executor;
    private readonly PresetStore _presetStore;
    private readonly Action<string, bool> _notifyStatus;
    private readonly Action _refreshUndoState;

    private string _folderPath = string.Empty;
    private OrganizeMode _selectedMode = OrganizeMode.ByCategory;
    private DateGroupingFormat _selectedDateFormat = DateGroupingFormat.YearMonth;
    private bool _includeSubdirectories;
    private bool _ignoreActiveDownloads = true;
    private string _newRuleFolder = string.Empty;
    private string _newRuleKeywords = string.Empty;
    private string _newRuleExtensions = string.Empty;
    private string _previewSummary = "Select a folder (e.g. Downloads) and click Scan & Preview.";
    private OperationPlan? _currentPlan;
    private IReadOnlyList<FileEntry> _scannedFiles = Array.Empty<FileEntry>();

    public OrganizeViewModel(
        BatchExecutor executor,
        PresetStore presetStore,
        Action<string, bool> notifyStatus,
        Action refreshUndoState)
    {
        _executor = executor;
        _presetStore = presetStore;
        _notifyStatus = notifyStatus;
        _refreshUndoState = refreshUndoState;

        SelectDownloadsCommand = new RelayCommand(SelectDownloads);
        ScanAndPreviewCommand = new RelayCommand(ScanAndPreview, () => Directory.Exists(FolderPath));
        AddRuleCommand = new RelayCommand(AddCustomRule, () => !string.IsNullOrWhiteSpace(NewRuleFolder));
        RemoveRuleCommand = new RelayCommand(p => RemoveCustomRule(p as CustomRuleItem), p => p is CustomRuleItem);
        ApplyCommand = new RelayCommand(ApplyPlan, () => CurrentPlan is { ReadyCount: > 0 });

        LoadCustomRules();
        SelectDownloads();
    }

    public ObservableCollection<PreviewOperationRow> PreviewRows { get; } = new();

    public ObservableCollection<CustomRuleItem> CustomRules { get; } = new();

    public IReadOnlyList<OrganizeMode> AvailableModes { get; } = Enum.GetValues<OrganizeMode>();

    public IReadOnlyList<DateGroupingFormat> AvailableDateFormats { get; } = Enum.GetValues<DateGroupingFormat>();

    public RelayCommand SelectDownloadsCommand { get; }
    public RelayCommand ScanAndPreviewCommand { get; }
    public RelayCommand AddRuleCommand { get; }
    public RelayCommand RemoveRuleCommand { get; }
    public RelayCommand ApplyCommand { get; }

    public string FolderPath
    {
        get => _folderPath;
        set
        {
            if (SetProperty(ref _folderPath, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public OrganizeMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value))
            {
                OnPropertyChanged(nameof(IsDateMode));
                RebuildPreviewFromScanned();
            }
        }
    }

    public DateGroupingFormat SelectedDateFormat
    {
        get => _selectedDateFormat;
        set
        {
            if (SetProperty(ref _selectedDateFormat, value))
            {
                RebuildPreviewFromScanned();
            }
        }
    }

    public bool IsDateMode => SelectedMode == OrganizeMode.ByDate;

    public bool IncludeSubdirectories
    {
        get => _includeSubdirectories;
        set
        {
            if (SetProperty(ref _includeSubdirectories, value))
            {
                ScanAndPreview();
            }
        }
    }

    public bool IgnoreActiveDownloads
    {
        get => _ignoreActiveDownloads;
        set
        {
            if (SetProperty(ref _ignoreActiveDownloads, value))
            {
                ScanAndPreview();
            }
        }
    }

    public string NewRuleFolder
    {
        get => _newRuleFolder;
        set
        {
            if (SetProperty(ref _newRuleFolder, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string NewRuleKeywords
    {
        get => _newRuleKeywords;
        set => SetProperty(ref _newRuleKeywords, value);
    }

    public string NewRuleExtensions
    {
        get => _newRuleExtensions;
        set => SetProperty(ref _newRuleExtensions, value);
    }

    public string PreviewSummary
    {
        get => _previewSummary;
        private set => SetProperty(ref _previewSummary, value);
    }

    public OperationPlan? CurrentPlan
    {
        get => _currentPlan;
        private set
        {
            if (SetProperty(ref _currentPlan, value))
            {
                OnPropertyChanged(nameof(CanApply));
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanApply => CurrentPlan is { ReadyCount: > 0 };

    public void SelectDownloads()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(userProfile, "Downloads");
        FolderPath = Directory.Exists(downloads) ? downloads : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        ScanAndPreview();
    }

    public void ScanAndPreview()
    {
        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
        {
            _scannedFiles = Array.Empty<FileEntry>();
            PreviewRows.Clear();
            CurrentPlan = null;
            PreviewSummary = "Folder not found. Choose an existing directory.";
            return;
        }

        try
        {
            _scannedFiles = _scanner.Scan(FolderPath, new FolderScanOptions
            {
                IncludeSubdirectories = IncludeSubdirectories,
                IgnoreActiveDownloads = IgnoreActiveDownloads,
                SortOrder = FileSortOrder.NameAsc
            });

            RebuildPreviewFromScanned();
        }
        catch (Exception ex)
        {
            _notifyStatus($"Failed to scan folder: {ex.Message}", true);
        }
    }

    public void RebuildPreviewFromScanned()
    {
        PreviewRows.Clear();

        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
        {
            CurrentPlan = null;
            return;
        }

        var options = new OrganizeOptions
        {
            Mode = SelectedMode,
            DateFormat = SelectedDateFormat,
            CustomRules = CustomRules.Select(r => r.ToCoreRule()).ToList()
        };

        var plan = _organizer.BuildPlan(FolderPath, _scannedFiles, options);
        CurrentPlan = plan;

        for (var i = 0; i < plan.Operations.Count; i++)
        {
            PreviewRows.Add(PreviewOperationRow.FromPlanned(i + 1, plan.Operations[i], FolderPath));
        }

        PreviewSummary =
            $"Scanned {_scannedFiles.Count} file(s) · Ready: {plan.ReadyCount} · Auto-Numbered: {plan.ConflictResolvedCount} · Skipped: {plan.SkippedCount}";
    }

    private void AddCustomRule()
    {
        if (string.IsNullOrWhiteSpace(NewRuleFolder))
        {
            return;
        }

        CustomRules.Add(new CustomRuleItem(NewRuleFolder.Trim(), NewRuleKeywords.Trim(), NewRuleExtensions.Trim()));
        NewRuleFolder = string.Empty;
        NewRuleKeywords = string.Empty;
        NewRuleExtensions = string.Empty;

        SaveCustomRules();
        RebuildPreviewFromScanned();
    }

    private void RemoveCustomRule(CustomRuleItem? item)
    {
        if (item is null)
        {
            return;
        }

        CustomRules.Remove(item);
        SaveCustomRules();
        RebuildPreviewFromScanned();
    }

    private void LoadCustomRules()
    {
        var data = _presetStore.Load();
        CustomRules.Clear();
        foreach (var rule in data.CustomOrganizeRules)
        {
            CustomRules.Add(CustomRuleItem.FromCoreRule(rule));
        }
    }

    private void SaveCustomRules()
    {
        var existing = _presetStore.Load();
        var updated = existing with
        {
            CustomOrganizeRules = CustomRules.Select(r => r.ToCoreRule()).ToList()
        };
        _presetStore.Save(updated);
    }

    private void ApplyPlan()
    {
        if (CurrentPlan is null || CurrentPlan.ReadyCount == 0)
        {
            return;
        }

        var result = _executor.Execute(CurrentPlan);
        _refreshUndoState();

        if (result.FailedCount > 0)
        {
            _notifyStatus(
                $"Organized {result.AppliedCount} file(s), {result.FailedCount} failed: {string.Join("; ", result.Errors)}",
                true);
        }
        else
        {
            _notifyStatus(
                $"Organized {result.AppliedCount} file(s) cleanly. You can undo this batch anytime.",
                false);
        }

        ScanAndPreview();
    }
}
