using System.Collections.ObjectModel;
using System.IO;
using TidyFlow.App.Services;
using TidyFlow.Core.Duplicates;
using TidyFlow.Core.Models;

namespace TidyFlow.App.ViewModels;

public sealed class DuplicateFileRow : ObservableObject
{
    private bool _isSelectedForDeletion;

    public DuplicateFileRow(
        int groupNumber,
        string shortHash,
        long sizeBytes,
        FileEntry entry,
        bool isSelectedForDeletion)
    {
        GroupNumber = groupNumber;
        ShortHash = shortHash;
        SizeBytes = sizeBytes;
        Entry = entry;
        _isSelectedForDeletion = isSelectedForDeletion;
    }

    public int GroupNumber { get; }

    public string ShortHash { get; }

    public long SizeBytes { get; }

    public string SizeText => RecycleBinService.FormatBytes(SizeBytes);

    public FileEntry Entry { get; }

    public string FileName => Entry.Name;

    public string FolderPath => Entry.DirectoryPath;

    public string ModifiedText => Entry.LastWriteTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

    public bool IsSelectedForDeletion
    {
        get => _isSelectedForDeletion;
        set
        {
            if (SetProperty(ref _isSelectedForDeletion, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }
}

public sealed class DuplicatesViewModel : ObservableObject
{
    private readonly DuplicateFinder _finder = new();
    private readonly Action<string, bool> _notifyStatus;

    private string _folderPath = string.Empty;
    private bool _includeSubdirectories = true;
    private bool _isScanning;
    private string _scanSummary = "Choose a folder and click Scan Duplicates to find identical files (SHA-256).";

    public DuplicatesViewModel(Action<string, bool> notifyStatus)
    {
        _notifyStatus = notifyStatus;

        SelectDownloadsCommand = new RelayCommand(SelectDownloads);
        ScanCommand = new RelayCommand(ScanAsync, () => !IsScanning && Directory.Exists(FolderPath));
        KeepOldestCommand = new RelayCommand(() => ApplySelectionPreset(keepOldest: true), () => DuplicateRows.Count > 0);
        KeepNewestCommand = new RelayCommand(() => ApplySelectionPreset(keepOldest: false), () => DuplicateRows.Count > 0);
        UncheckAllCommand = new RelayCommand(UncheckAll, () => DuplicateRows.Any(r => r.IsSelectedForDeletion));
        DeleteCheckedCommand = new RelayCommand(DeleteChecked, () => !IsScanning && DuplicateRows.Any(r => r.IsSelectedForDeletion));

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(userProfile, "Downloads");
        _folderPath = Directory.Exists(downloads) ? downloads : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    public ObservableCollection<DuplicateFileRow> DuplicateRows { get; } = new();

    public RelayCommand SelectDownloadsCommand { get; }
    public RelayCommand ScanCommand { get; }
    public RelayCommand KeepOldestCommand { get; }
    public RelayCommand KeepNewestCommand { get; }
    public RelayCommand UncheckAllCommand { get; }
    public RelayCommand DeleteCheckedCommand { get; }

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

    public bool IncludeSubdirectories
    {
        get => _includeSubdirectories;
        set => SetProperty(ref _includeSubdirectories, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ScanSummary
    {
        get => _scanSummary;
        private set => SetProperty(ref _scanSummary, value);
    }

    public int CheckedCount => DuplicateRows.Count(r => r.IsSelectedForDeletion);

    public long CheckedBytes => DuplicateRows.Where(r => r.IsSelectedForDeletion).Sum(r => r.SizeBytes);

    public void SelectDownloads()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(userProfile, "Downloads");
        FolderPath = Directory.Exists(downloads) ? downloads : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        ScanAsync();
    }

    public async void ScanAsync()
    {
        if (IsScanning || string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
        {
            return;
        }

        IsScanning = true;
        ScanSummary = "Scanning and hashing duplicate candidates...";
        DuplicateRows.Clear();

        try
        {
            var targetPath = FolderPath;
            var recursive = IncludeSubdirectories;
            var result = await Task.Run(() => _finder.Scan(targetPath, recursive));

            for (var i = 0; i < result.Groups.Count; i++)
            {
                var group = result.Groups[i];
                var shortHash = group.Sha256Hash[..Math.Min(8, group.Sha256Hash.Length)];

                // Default selection: keep the oldest file (index 0) and mark newer copies for deletion.
                for (var j = 0; j < group.Files.Count; j++)
                {
                    var row = new DuplicateFileRow(
                        i + 1,
                        shortHash,
                        group.FileSizeBytes,
                        group.Files[j],
                        isSelectedForDeletion: j > 0);
                    row.PropertyChanged += (_, _) => UpdateSelectionSummary();
                    DuplicateRows.Add(row);
                }
            }

            ScanSummary = result.Groups.Count == 0
                ? $"Scanned {result.TotalFilesScanned} file(s). No duplicate content found."
                : $"Scanned {result.TotalFilesScanned} file(s) · {result.Groups.Count} duplicate group(s) · Potential savings: {RecycleBinService.FormatBytes(result.TotalWastedBytes)}";

            RelayCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _notifyStatus($"Duplicate scan failed: {ex.Message}", true);
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void ApplySelectionPreset(bool keepOldest)
    {
        foreach (var group in DuplicateRows.GroupBy(r => r.GroupNumber))
        {
            var ordered = keepOldest
                ? group.OrderBy(r => r.Entry.LastWriteTime).ToList()
                : group.OrderByDescending(r => r.Entry.LastWriteTime).ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].IsSelectedForDeletion = i > 0;
            }
        }

        UpdateSelectionSummary();
    }

    private void UncheckAll()
    {
        foreach (var row in DuplicateRows)
        {
            row.IsSelectedForDeletion = false;
        }

        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        OnPropertyChanged(nameof(CheckedCount));
        OnPropertyChanged(nameof(CheckedBytes));
        RelayCommand.RaiseCanExecuteChanged();
    }

    private void DeleteChecked()
    {
        var targets = DuplicateRows.Where(r => r.IsSelectedForDeletion).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var (deleted, errors) = RecycleBinService.MoveFilesToRecycleBin(targets.Select(t => t.Entry.FullPath));
        var freedBytes = targets.Take(deleted).Sum(t => t.SizeBytes);

        if (errors.Count > 0)
        {
            _notifyStatus($"Moved {deleted} duplicate(s) to Recycle Bin, {errors.Count} failed: {string.Join("; ", errors)}", true);
        }
        else
        {
            _notifyStatus($"Moved {deleted} duplicate file(s) to Recycle Bin (~{RecycleBinService.FormatBytes(freedBytes)} freed).", false);
        }

        ScanAsync();
    }
}
