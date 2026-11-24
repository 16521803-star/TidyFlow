using System.Collections.ObjectModel;
using System.IO;
using TidyFlow.App.Models;
using TidyFlow.App.Services;
using TidyFlow.Core.Imaging;
using TidyFlow.Core.Models;
using TidyFlow.Core.Scanning;

namespace TidyFlow.App.ViewModels;

public sealed class ImageResizerViewModel : ObservableObject
{
    private readonly FolderScanner _scanner = new();
    private readonly ImageResizePlanner _planner = new();
    private readonly Action<string, bool> _notifyStatus;

    private ImageResizeMode _selectedMode = ImageResizeMode.MaxWidth;
    private ImageOutputFormat _selectedFormat = ImageOutputFormat.KeepOriginal;
    private string _targetWidthText = "1920";
    private string _targetHeightText = "1080";
    private string _percentageText = "50";
    private int _jpegQuality = 90;
    private bool _doNotUpscale = true;
    private string _suffix = "_resized";
    private string _outputFolderPath = string.Empty;
    private bool _isProcessing;
    private string _previewSummary = "Add or drop images (JPG, PNG, BMP, TIFF) to resize or convert.";
    private IReadOnlyList<PlannedImageOperation> _currentPlan = Array.Empty<PlannedImageOperation>();

    public ImageResizerViewModel(Action<string, bool> notifyStatus)
    {
        _notifyStatus = notifyStatus;

        ClearFilesCommand = new RelayCommand(ClearFiles, () => Files.Count > 0);
        ClearOutputFolderCommand = new RelayCommand(() => OutputFolderPath = string.Empty, () => !string.IsNullOrWhiteSpace(OutputFolderPath));
        ApplyCommand = new RelayCommand(ApplyAsync, () => !IsProcessing && _currentPlan.Any(p => p.Status is OperationStatus.Ready or OperationStatus.ConflictResolved));
    }

    public ObservableCollection<FileEntry> Files { get; } = new();

    public ObservableCollection<PreviewOperationRow> PreviewRows { get; } = new();

    public IReadOnlyList<ImageResizeMode> AvailableModes { get; } = Enum.GetValues<ImageResizeMode>();

    public IReadOnlyList<LocalizedOption<ImageResizeMode>> AvailableModeOptions => new[]
    {
        new LocalizedOption<ImageResizeMode>(ImageResizeMode.MaxWidth, LocalizationService.Instance.Get("Str_Enum_MaxWidth")),
        new LocalizedOption<ImageResizeMode>(ImageResizeMode.MaxHeight, LocalizationService.Instance.Get("Str_Enum_MaxHeight")),
        new LocalizedOption<ImageResizeMode>(ImageResizeMode.FitWithinBox, LocalizationService.Instance.Get("Str_Enum_FitWithinBox")),
        new LocalizedOption<ImageResizeMode>(ImageResizeMode.Percentage, LocalizationService.Instance.Get("Str_Enum_Percentage")),
        new LocalizedOption<ImageResizeMode>(ImageResizeMode.CropToExact, LocalizationService.Instance.Get("Str_Enum_CropToExact")),
        new LocalizedOption<ImageResizeMode>(ImageResizeMode.ExactStretch, LocalizationService.Instance.Get("Str_Enum_ExactStretch"))
    };

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(AvailableModeOptions));
        RebuildPreview();
    }

    public IReadOnlyList<ImageOutputFormat> AvailableFormats { get; } = Enum.GetValues<ImageOutputFormat>();

    public RelayCommand ClearFilesCommand { get; }
    public RelayCommand ClearOutputFolderCommand { get; }
    public RelayCommand ApplyCommand { get; }

    public ImageResizeMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value))
            {
                OnPropertyChanged(nameof(IsWidthEnabled));
                OnPropertyChanged(nameof(IsHeightEnabled));
                OnPropertyChanged(nameof(IsPercentageEnabled));
                OnPropertyChanged(nameof(IsDoNotUpscaleEnabled));
                RebuildPreview();
            }
        }
    }

    public bool IsWidthEnabled => SelectedMode is ImageResizeMode.MaxWidth
        or ImageResizeMode.FitWithinBox
        or ImageResizeMode.CropToExact
        or ImageResizeMode.ExactStretch;

    public bool IsHeightEnabled => SelectedMode is ImageResizeMode.MaxHeight
        or ImageResizeMode.FitWithinBox
        or ImageResizeMode.CropToExact
        or ImageResizeMode.ExactStretch;

    public bool IsPercentageEnabled => SelectedMode == ImageResizeMode.Percentage;

    public bool IsDoNotUpscaleEnabled => SelectedMode is ImageResizeMode.MaxWidth
        or ImageResizeMode.MaxHeight
        or ImageResizeMode.FitWithinBox;

    public ImageOutputFormat SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (SetProperty(ref _selectedFormat, value))
            {
                RebuildPreview();
            }
        }
    }

    public string TargetWidthText
    {
        get => _targetWidthText;
        set
        {
            if (SetProperty(ref _targetWidthText, value))
            {
                RebuildPreview();
            }
        }
    }

    public string TargetHeightText
    {
        get => _targetHeightText;
        set
        {
            if (SetProperty(ref _targetHeightText, value))
            {
                RebuildPreview();
            }
        }
    }

    public string PercentageText
    {
        get => _percentageText;
        set
        {
            if (SetProperty(ref _percentageText, value))
            {
                RebuildPreview();
            }
        }
    }

    public int JpegQuality
    {
        get => _jpegQuality;
        set => SetProperty(ref _jpegQuality, value);
    }

    public bool DoNotUpscale
    {
        get => _doNotUpscale;
        set
        {
            if (SetProperty(ref _doNotUpscale, value))
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

    public bool IsProcessing
    {
        get => _isProcessing;
        private set
        {
            if (SetProperty(ref _isProcessing, value))
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

    public void AddPaths(IEnumerable<string> paths)
    {
        var existing = Files.Select(f => f.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                foreach (var entry in _scanner.Scan(path))
                {
                    if (ImageResizePlanner.IsSupportedImage(entry.FullPath) && existing.Add(entry.FullPath))
                    {
                        Files.Add(entry);
                        added++;
                    }
                }
            }
            else if (File.Exists(path) && ImageResizePlanner.IsSupportedImage(path) && existing.Add(path))
            {
                Files.Add(FileEntry.FromFileInfo(new FileInfo(path)));
                added++;
            }
        }

        if (added > 0)
        {
            RebuildPreview();
            RelayCommand.RaiseCanExecuteChanged();
        }
    }

    public void RebuildPreview()
    {
        PreviewRows.Clear();
        if (Files.Count == 0)
        {
            _currentPlan = Array.Empty<PlannedImageOperation>();
            PreviewSummary = "Add or drop images (JPG, PNG, BMP, TIFF) to resize or convert.";
            RelayCommand.RaiseCanExecuteChanged();
            return;
        }

        var options = BuildOptions();
        _currentPlan = _planner.BuildPlan(Files.ToList(), options);

        for (var i = 0; i < _currentPlan.Count; i++)
        {
            var op = _currentPlan[i];
            var targetDisplay = string.IsNullOrWhiteSpace(options.OutputDirectory)
                ? Path.GetFileName(op.DestinationPath)
                : $"{Path.GetFileName(Path.GetDirectoryName(op.DestinationPath))}\\{Path.GetFileName(op.DestinationPath)}";

            var statusText = PreviewOperationRow.LocalizeStatus(op.Status);

            PreviewRows.Add(new PreviewOperationRow(
                i + 1,
                statusText,
                op.Status,
                Path.GetFileName(op.SourcePath),
                targetDisplay,
                op.Note ?? string.Empty));
        }

        var ready = _currentPlan.Count(p => p.Status is OperationStatus.Ready or OperationStatus.ConflictResolved);
        var skipped = _currentPlan.Count - ready;
        PreviewSummary = $"Images: {Files.Count} · Ready: {ready} · Skipped: {skipped}";
        RelayCommand.RaiseCanExecuteChanged();
    }

    private ImageResizeOptions BuildOptions()
    {
        _ = int.TryParse(TargetWidthText, out var w);
        _ = int.TryParse(TargetHeightText, out var h);
        _ = int.TryParse(PercentageText, out var pct);

        return new ImageResizeOptions
        {
            Mode = SelectedMode,
            TargetWidth = w > 0 ? w : 1920,
            TargetHeight = h > 0 ? h : 1080,
            Percentage = pct > 0 ? pct : 50,
            DoNotUpscale = DoNotUpscale,
            OutputFormat = SelectedFormat,
            JpegQuality = JpegQuality,
            Suffix = Suffix,
            OutputDirectory = OutputFolderPath
        };
    }

    private void ClearFiles()
    {
        Files.Clear();
        RebuildPreview();
    }

    private async void ApplyAsync()
    {
        if (IsProcessing || _currentPlan.Count == 0)
        {
            return;
        }

        IsProcessing = true;
        try
        {
            var planSnapshot = _currentPlan;
            var options = BuildOptions();
            var result = await Task.Run(() => ImageResizerService.Execute(planSnapshot, options));

            if (result.FailedCount > 0)
            {
                _notifyStatus($"Processed {result.ProcessedCount} image(s), {result.FailedCount} failed: {string.Join("; ", result.Errors)}", true);
            }
            else
            {
                _notifyStatus($"Resized/converted {result.ProcessedCount} image(s) successfully.", false);
            }

            RebuildPreview();
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
