using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using TidyFlow.App.Models;
using TidyFlow.Core.Models;
using TidyFlow.Core.Presets;
using TidyFlow.Core.Renaming;
using TidyFlow.Core.Safety;
using TidyFlow.Core.Scanning;

namespace TidyFlow.App.ViewModels;

public sealed class RenameViewModel : ObservableObject
{
    private readonly FolderScanner _scanner = new();
    private readonly BatchRenamer _renamer = new();
    private readonly BatchExecutor _executor;
    private readonly PresetStore _presetStore;
    private readonly Action<string, bool> _notifyStatus;
    private readonly Action _refreshUndoState;

    private bool _useListMode = true;
    private string _rawNameListText = string.Empty;
    private bool _preserveOriginalExtension = true;
    private bool _cleanDuplicateMarkers;
    private string _prefix = string.Empty;
    private string _suffix = string.Empty;
    private ListMismatchBehavior _mismatchBehavior = ListMismatchBehavior.SkipExtraFiles;
    private string _templatePattern = "{index}_{name}";
    private string _findText = string.Empty;
    private string _replaceText = string.Empty;
    private string _newListPresetName = string.Empty;
    private string _outputFolderPath = string.Empty;
    private SavedNameList? _selectedSavedList;
    private OrderedRenameFileItem? _selectedFileItem;
    private string _previewSummary = "Add files (or drag & drop from Explorer) and paste a list of names to preview.";
    private OperationPlan? _currentPlan;

    public RenameViewModel(
        BatchExecutor executor,
        PresetStore presetStore,
        Action<string, bool> notifyStatus,
        Action refreshUndoState)
    {
        _executor = executor;
        _presetStore = presetStore;
        _notifyStatus = notifyStatus;
        _refreshUndoState = refreshUndoState;

        MoveUpCommand = new RelayCommand(MoveSelectedUp, CanMoveSelectedUp);
        MoveDownCommand = new RelayCommand(MoveSelectedDown, CanMoveSelectedDown);
        SortByNameCommand = new RelayCommand(() => SortFiles(byDate: false), () => Files.Count > 1);
        SortByDateCommand = new RelayCommand(() => SortFiles(byDate: true), () => Files.Count > 1);
        RemoveSelectedCommand = new RelayCommand(RemoveSelectedFile, () => SelectedFileItem is not null);
        ClearFilesCommand = new RelayCommand(ClearFiles, () => Files.Count > 0);
        PasteClipboardListCommand = new RelayCommand(PasteClipboardList);
        SuggestNamesFromOcrCommand = new RelayCommand(SuggestNamesFromOcrAsync, () => Files.Count > 0);
        SaveCurrentListCommand = new RelayCommand(SaveCurrentListPreset, () => !string.IsNullOrWhiteSpace(NewListPresetName) && !string.IsNullOrWhiteSpace(RawNameListText));
        DeleteSavedListCommand = new RelayCommand(DeleteSelectedSavedList, () => SelectedSavedList is not null);
        ClearOutputFolderCommand = new RelayCommand(() => OutputFolderPath = string.Empty, () => !string.IsNullOrWhiteSpace(OutputFolderPath));
        ApplyCommand = new RelayCommand(ApplyPlan, () => CurrentPlan is { ReadyCount: > 0, HasErrors: false });

        LoadSavedLists();
    }

    public ObservableCollection<OrderedRenameFileItem> Files { get; } = new();

    public ObservableCollection<PreviewOperationRow> PreviewRows { get; } = new();

    public ObservableCollection<SavedNameList> SavedLists { get; } = new();

    public IReadOnlyList<ListMismatchBehavior> AvailableMismatchBehaviors { get; } = Enum.GetValues<ListMismatchBehavior>();

    public IReadOnlyList<Services.LocalizedOption<ListMismatchBehavior>> AvailableMismatchOptions => new[]
    {
        new Services.LocalizedOption<ListMismatchBehavior>(ListMismatchBehavior.SkipExtraFiles, Services.LocalizationService.Instance.Get("Str_Enum_SkipExtraFiles")),
        new Services.LocalizedOption<ListMismatchBehavior>(ListMismatchBehavior.PadWithOriginalNameAndIndex, Services.LocalizationService.Instance.Get("Str_Enum_PadWithOriginalNameAndIndex")),
        new Services.LocalizedOption<ListMismatchBehavior>(ListMismatchBehavior.PadWithPrefixAndIndex, Services.LocalizationService.Instance.Get("Str_Enum_PadWithPrefixAndIndex"))
    };

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(AvailableMismatchOptions));
        OnPropertyChanged(nameof(ParsedListCountText));
        RebuildPreview();
    }

    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand SortByNameCommand { get; }
    public RelayCommand SortByDateCommand { get; }
    public RelayCommand RemoveSelectedCommand { get; }
    public RelayCommand ClearFilesCommand { get; }
    public RelayCommand PasteClipboardListCommand { get; }
    public RelayCommand SuggestNamesFromOcrCommand { get; }
    public RelayCommand SaveCurrentListCommand { get; }
    public RelayCommand DeleteSavedListCommand { get; }
    public RelayCommand ClearOutputFolderCommand { get; }
    public RelayCommand ApplyCommand { get; }

    public bool UseListMode
    {
        get => _useListMode;
        set
        {
            if (SetProperty(ref _useListMode, value))
            {
                OnPropertyChanged(nameof(UseTemplateMode));
                RebuildPreview();
            }
        }
    }

    public bool UseTemplateMode
    {
        get => !UseListMode;
        set => UseListMode = !value;
    }

    public string RawNameListText
    {
        get => _rawNameListText;
        set
        {
            if (SetProperty(ref _rawNameListText, value))
            {
                OnPropertyChanged(nameof(ParsedListCountText));
                RelayCommand.RaiseCanExecuteChanged();
                RebuildPreview();
            }
        }
    }

    public string ParsedListCountText =>
        Services.LocalizationService.Instance.Format(
            "Str_ParsedListCountFmt",
            ListRenameOptions.ParseRawNameList(RawNameListText).Count,
            Files.Count);

    public bool PreserveOriginalExtension
    {
        get => _preserveOriginalExtension;
        set
        {
            if (SetProperty(ref _preserveOriginalExtension, value))
            {
                RebuildPreview();
            }
        }
    }

    public bool CleanDuplicateMarkers
    {
        get => _cleanDuplicateMarkers;
        set
        {
            if (SetProperty(ref _cleanDuplicateMarkers, value))
            {
                RebuildPreview();
            }
        }
    }

    public string Prefix
    {
        get => _prefix;
        set
        {
            if (SetProperty(ref _prefix, value))
            {
                RebuildPreview();
            }
        }
    }

    public string Suffix
    {
        get => _suffix;
        set
        {
            if (SetProperty(ref _suffix, value))
            {
                RebuildPreview();
            }
        }
    }

    public ListMismatchBehavior MismatchBehavior
    {
        get => _mismatchBehavior;
        set
        {
            if (SetProperty(ref _mismatchBehavior, value))
            {
                RebuildPreview();
            }
        }
    }

    public string TemplatePattern
    {
        get => _templatePattern;
        set
        {
            if (SetProperty(ref _templatePattern, value))
            {
                RebuildPreview();
            }
        }
    }

    public string FindText
    {
        get => _findText;
        set
        {
            if (SetProperty(ref _findText, value))
            {
                RebuildPreview();
            }
        }
    }

    public string ReplaceText
    {
        get => _replaceText;
        set
        {
            if (SetProperty(ref _replaceText, value))
            {
                RebuildPreview();
            }
        }
    }

    public string NewListPresetName
    {
        get => _newListPresetName;
        set
        {
            if (SetProperty(ref _newListPresetName, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string OutputFolderPath
    {
        get => _outputFolderPath;
        set
        {
            if (SetProperty(ref _outputFolderPath, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
                RebuildPreview();
            }
        }
    }

    public SavedNameList? SelectedSavedList
    {
        get => _selectedSavedList;
        set
        {
            if (SetProperty(ref _selectedSavedList, value))
            {
                if (value is not null)
                {
                    RawNameListText = string.Join(Environment.NewLine, value.Items);
                }

                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public OrderedRenameFileItem? SelectedFileItem
    {
        get => _selectedFileItem;
        set
        {
            if (SetProperty(ref _selectedFileItem, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
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

    public bool CanApply => CurrentPlan is { ReadyCount: > 0, HasErrors: false };

    public void AddPaths(IEnumerable<string> paths)
    {
        var existingPaths = Files
            .Select(f => f.Entry.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                foreach (var scanned in _scanner.Scan(path))
                {
                    if (existingPaths.Add(scanned.FullPath))
                    {
                        Files.Add(new OrderedRenameFileItem(Files.Count + 1, scanned));
                        added++;
                    }
                }
            }
            else if (File.Exists(path) && existingPaths.Add(path))
            {
                Files.Add(new OrderedRenameFileItem(Files.Count + 1, FileEntry.FromFileInfo(new FileInfo(path))));
                added++;
            }
        }

        if (added > 0)
        {
            ReindexFiles();
            RebuildPreview();
        }
    }

    public void RebuildPreview()
    {
        PreviewRows.Clear();
        OnPropertyChanged(nameof(ParsedListCountText));

        if (Files.Count == 0)
        {
            CurrentPlan = null;
            PreviewSummary = "Add files (or drag & drop from Explorer) and paste a list of names to preview.";
            return;
        }

        var orderedEntries = Files.Select(f => f.Entry).ToList();
        OperationPlan plan;

        if (UseListMode)
        {
            var targetNames = ListRenameOptions.ParseRawNameList(RawNameListText);
            plan = _renamer.BuildListRenamePlan(orderedEntries, new ListRenameOptions
            {
                TargetNames = targetNames,
                PreserveOriginalExtension = PreserveOriginalExtension,
                CleanDuplicateMarkers = CleanDuplicateMarkers,
                Prefix = Prefix,
                Suffix = Suffix,
                MismatchBehavior = MismatchBehavior,
                OutputDirectory = OutputFolderPath
            });
        }
        else
        {
            plan = _renamer.BuildTemplateRenamePlan(orderedEntries, new TemplateRenameOptions
            {
                Pattern = TemplatePattern,
                FindText = FindText,
                ReplaceText = ReplaceText,
                CleanDuplicateMarkers = CleanDuplicateMarkers,
                OutputDirectory = OutputFolderPath
            });
        }

        CurrentPlan = plan;
        for (var i = 0; i < plan.Operations.Count; i++)
        {
            PreviewRows.Add(PreviewOperationRow.FromPlanned(i + 1, plan.Operations[i]));
        }

        PreviewSummary = Services.LocalizationService.Instance.Format(
            "Str_RenameSummaryFmt",
            Files.Count,
            plan.ReadyCount,
            plan.ConflictResolvedCount,
            plan.SkippedCount,
            plan.ErrorCount);
    }

    private bool CanMoveSelectedUp()
    {
        if (SelectedFileItem is null)
        {
            return false;
        }

        return Files.IndexOf(SelectedFileItem) > 0;
    }

    private void MoveSelectedUp()
    {
        if (SelectedFileItem is null)
        {
            return;
        }

        var idx = Files.IndexOf(SelectedFileItem);
        if (idx <= 0)
        {
            return;
        }

        Files.Move(idx, idx - 1);
        ReindexFiles(keepSelectedPath: Files[idx - 1].Entry.FullPath);
        RebuildPreview();
    }

    private bool CanMoveSelectedDown()
    {
        if (SelectedFileItem is null)
        {
            return false;
        }

        var idx = Files.IndexOf(SelectedFileItem);
        return idx >= 0 && idx < Files.Count - 1;
    }

    private void MoveSelectedDown()
    {
        if (SelectedFileItem is null)
        {
            return;
        }

        var idx = Files.IndexOf(SelectedFileItem);
        if (idx < 0 || idx >= Files.Count - 1)
        {
            return;
        }

        Files.Move(idx, idx + 1);
        ReindexFiles(keepSelectedPath: Files[idx + 1].Entry.FullPath);
        RebuildPreview();
    }

    private void SortFiles(bool byDate)
    {
        var sorted = byDate
            ? Files.Select(f => f.Entry).OrderBy(e => e.LastWriteTime).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList()
            : Files.Select(f => f.Entry).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();

        Files.Clear();
        for (var i = 0; i < sorted.Count; i++)
        {
            Files.Add(new OrderedRenameFileItem(i + 1, sorted[i]));
        }

        RebuildPreview();
    }

    private void RemoveSelectedFile()
    {
        if (SelectedFileItem is null)
        {
            return;
        }

        Files.Remove(SelectedFileItem);
        ReindexFiles();
        RebuildPreview();
    }

    private void ClearFiles()
    {
        Files.Clear();
        RebuildPreview();
    }

    private void ReindexFiles(string? keepSelectedPath = null)
    {
        var entries = Files.Select(f => f.Entry).ToList();
        Files.Clear();

        OrderedRenameFileItem? toSelect = null;
        for (var i = 0; i < entries.Count; i++)
        {
            var item = new OrderedRenameFileItem(i + 1, entries[i]);
            Files.Add(item);

            if (keepSelectedPath is not null &&
                string.Equals(entries[i].FullPath, keepSelectedPath, StringComparison.OrdinalIgnoreCase))
            {
                toSelect = item;
            }
        }

        if (toSelect is not null)
        {
            SelectedFileItem = toSelect;
        }

        RelayCommand.RaiseCanExecuteChanged();
    }

    private void PasteClipboardList()
    {
        if (Clipboard.ContainsText())
        {
            RawNameListText = Clipboard.GetText();
        }
    }

    private async void SuggestNamesFromOcrAsync()
    {
        if (Files.Count == 0)
        {
            return;
        }

        var suggested = new List<string>(Files.Count);
        var recognizedCount = 0;

        foreach (var item in Files)
        {
            var ext = item.Entry.Extension.ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff")
            {
                try
                {
                    var text = await Services.WindowsOcrService.ExtractTextAsync(item.Entry.FullPath);
                    var stem = OcrNameSuggester.SuggestStem(text, item.Entry.NameWithoutExtension);
                    if (!string.Equals(stem, item.Entry.NameWithoutExtension, StringComparison.Ordinal))
                    {
                        recognizedCount++;
                    }

                    suggested.Add(stem);
                    continue;
                }
                catch
                {
                    // Fall back to original name if OCR fails on a single image.
                }
            }

            suggested.Add(item.Entry.NameWithoutExtension);
        }

        RawNameListText = string.Join(Environment.NewLine, suggested);
        UseListMode = true;
        _notifyStatus($"Windows OCR generated name suggestions for {recognizedCount} of {Files.Count} file(s).", false);
    }

    private void LoadSavedLists()
    {
        var data = _presetStore.Load();
        SavedLists.Clear();
        foreach (var list in data.SavedNameLists)
        {
            SavedLists.Add(list);
        }
    }

    private void SaveCurrentListPreset()
    {
        var items = ListRenameOptions.ParseRawNameList(RawNameListText);
        if (string.IsNullOrWhiteSpace(NewListPresetName) || items.Count == 0)
        {
            return;
        }

        var presetName = NewListPresetName.Trim();
        var existing = SavedLists.FirstOrDefault(l => string.Equals(l.Name, presetName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SavedLists.Remove(existing);
        }

        var created = new SavedNameList(presetName, items);
        SavedLists.Add(created);
        NewListPresetName = string.Empty;

        PersistSavedLists();
        SelectedSavedList = created;
        _notifyStatus($"Saved name list '{presetName}' ({items.Count} item(s)).", false);
    }

    private void DeleteSelectedSavedList()
    {
        if (SelectedSavedList is null)
        {
            return;
        }

        var removedName = SelectedSavedList.Name;
        SavedLists.Remove(SelectedSavedList);
        SelectedSavedList = null;
        PersistSavedLists();
        _notifyStatus($"Deleted saved list '{removedName}'.", false);
    }

    private void PersistSavedLists()
    {
        var current = _presetStore.Load();
        _presetStore.Save(current with { SavedNameLists = SavedLists.ToList() });
    }

    private void ApplyPlan()
    {
        if (CurrentPlan is null || CurrentPlan.ReadyCount == 0)
        {
            return;
        }

        var result = _executor.Execute(CurrentPlan);
        _refreshUndoState();

        // Refresh ordered file entries to reflect their new on-disk names.
        var updatedEntries = new List<FileEntry>(Files.Count);
        foreach (var item in Files)
        {
            var planned = CurrentPlan.Operations.FirstOrDefault(o =>
                string.Equals(o.SourcePath, item.Entry.FullPath, StringComparison.OrdinalIgnoreCase));

            var currentPath = planned is not null && planned.WillModify && File.Exists(planned.DestinationPath)
                ? planned.DestinationPath
                : item.Entry.FullPath;

            if (File.Exists(currentPath))
            {
                updatedEntries.Add(FileEntry.FromFileInfo(new FileInfo(currentPath)));
            }
        }

        Files.Clear();
        for (var i = 0; i < updatedEntries.Count; i++)
        {
            Files.Add(new OrderedRenameFileItem(i + 1, updatedEntries[i]));
        }

        if (result.FailedCount > 0)
        {
            _notifyStatus(
                $"Renamed {result.AppliedCount} file(s), {result.FailedCount} failed: {string.Join("; ", result.Errors)}",
                true);
        }
        else
        {
            _notifyStatus(
                $"Renamed {result.AppliedCount} file(s) cleanly. You can undo this batch anytime.",
                false);
        }

        RebuildPreview();
    }
}
