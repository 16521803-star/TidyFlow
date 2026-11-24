using System.Collections.ObjectModel;
using System.IO;
using TidyFlow.App.Services;
using TidyFlow.Core.Cleaning;
using TidyFlow.Core.Models;

namespace TidyFlow.App.ViewModels;

public sealed class LargeFileRow : ObservableObject
{
    private bool _isSelectedForDeletion;

    public LargeFileRow(int rank, FileEntry entry)
    {
        Rank = rank;
        Entry = entry;
    }

    public int Rank { get; }

    public FileEntry Entry { get; }

    public string Name => Entry.Name;

    public string SizeText => RecycleBinService.FormatBytes(Entry.SizeBytes);

    public string ModifiedText => Entry.LastWriteTime.LocalDateTime.ToString("yyyy-MM-dd");

    public string FolderPath => Entry.DirectoryPath;

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

public sealed record SubfolderSizeRow(
    string Name,
    int FileCount,
    string SizeText);

public sealed class SpaceCleanerViewModel : ObservableObject
{
    private readonly SpaceCleaner _cleaner = new();
    private readonly Action<string, bool> _notifyStatus;

    private string _folderPath = string.Empty;
    private bool _isScanning;
    private string _scanSummary = "Scan a folder to find largest files, heavy subfolders, and empty directories.";
    private IReadOnlyList<string> _emptyFolderPaths = Array.Empty<string>();

    public SpaceCleanerViewModel(Action<string, bool> notifyStatus)
    {
        _notifyStatus = notifyStatus;

        SelectDownloadsCommand = new RelayCommand(SelectDownloads);
        ScanCommand = new RelayCommand(ScanAsync, () => !IsScanning && Directory.Exists(FolderPath));
        DeleteSelectedFilesCommand = new RelayCommand(DeleteSelectedFiles, () => !IsScanning && LargestFiles.Any(f => f.IsSelectedForDeletion));
        CleanEmptyFoldersCommand = new RelayCommand(CleanEmptyFolders, () => !IsScanning && _emptyFolderPaths.Count > 0);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(userProfile, "Downloads");
        _folderPath = Directory.Exists(downloads) ? downloads : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    public ObservableCollection<LargeFileRow> LargestFiles { get; } = new();

    public ObservableCollection<SubfolderSizeRow> TopSubfolders { get; } = new();

    public ObservableCollection<EmptyFolderEntry> EmptyFolders { get; } = new();

    public RelayCommand SelectDownloadsCommand { get; }
    public RelayCommand ScanCommand { get; }
    public RelayCommand DeleteSelectedFilesCommand { get; }
    public RelayCommand CleanEmptyFoldersCommand { get; }

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

    public int EmptyFolderCount => EmptyFolders.Count;

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
        ScanSummary = "Analyzing disk usage and empty folders...";
        LargestFiles.Clear();
        TopSubfolders.Clear();
        EmptyFolders.Clear();

        try
        {
            var target = FolderPath;
            var result = await Task.Run(() => _cleaner.Scan(target, topFilesCount: 100));

            for (var i = 0; i < result.LargestFiles.Count; i++)
            {
                LargestFiles.Add(new LargeFileRow(i + 1, result.LargestFiles[i]));
            }

            foreach (var sub in result.TopSubfolders)
            {
                TopSubfolders.Add(new SubfolderSizeRow(
                    sub.Name,
                    sub.FileCount,
                    RecycleBinService.FormatBytes(sub.TotalBytes)));
            }

            foreach (var empty in result.EmptyFolders)
            {
                EmptyFolders.Add(empty);
            }

            _emptyFolderPaths = result.EmptyFolders.Select(e => e.FullPath).ToList();
            OnPropertyChanged(nameof(EmptyFolderCount));

            ScanSummary =
                $"Total: {RecycleBinService.FormatBytes(result.TotalScannedBytes)} across {result.TotalFileCount} file(s) · Empty folders: {result.EmptyFolders.Count}";

            RelayCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _notifyStatus($"Space scan failed: {ex.Message}", true);
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void DeleteSelectedFiles()
    {
        var selected = LargestFiles.Where(f => f.IsSelectedForDeletion).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var totalBytes = selected.Sum(s => s.Entry.SizeBytes);
        var (deleted, errors) = RecycleBinService.MoveFilesToRecycleBin(selected.Select(s => s.Entry.FullPath));

        if (errors.Count > 0)
        {
            _notifyStatus($"Moved {deleted} file(s) to Recycle Bin, {errors.Count} failed: {string.Join("; ", errors)}", true);
        }
        else
        {
            _notifyStatus($"Moved {deleted} file(s) ({RecycleBinService.FormatBytes(totalBytes)}) to Recycle Bin.", false);
        }

        ScanAsync();
    }

    private void CleanEmptyFolders()
    {
        if (_emptyFolderPaths.Count == 0)
        {
            return;
        }

        var deleted = SpaceCleaner.DeleteEmptyFolders(_emptyFolderPaths);
        _notifyStatus($"Removed {deleted} empty folder(s).", false);
        ScanAsync();
    }
}
