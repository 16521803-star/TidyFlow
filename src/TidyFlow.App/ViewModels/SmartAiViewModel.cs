using System.Collections.ObjectModel;
using System.IO;
using TidyFlow.App.Models;
using TidyFlow.App.Services;
using TidyFlow.Core.Models;
using TidyFlow.Core.Safety;
using TidyFlow.Core.Scanning;
using TidyFlow.Core.Smart;

namespace TidyFlow.App.ViewModels;

public sealed class SmartAiViewModel : ObservableObject
{
    private readonly FolderScanner _scanner = new();
    private readonly AiOrganizePlanner _planner = new();
    private readonly AiSettingsStore _settingsStore = new();
    private readonly BatchExecutor _executor;
    private readonly Action<string, bool> _notifyStatus;
    private readonly Action _refreshUndoState;

    private string _folderPath = string.Empty;
    private string _promptInstruction = "Group files into clean folders by topic and file type (e.g. Finance, Work, Media, Installers, Archives)";
    private bool _useExternalAiEndpoint;
    private string _endpointUrl = "http://localhost:11434/v1/chat/completions";
    private string _modelName = "llama3.2";
    private string _apiKey = string.Empty;
    private bool _includeTextSnippets = true;
    private bool _isBusy;
    private string _previewSummary = "Enter an instruction and click Preview Smart Plan (works offline or with Ollama/OpenAI API).";
    private string _ocrExtractedText = string.Empty;
    private OperationPlan? _currentPlan;

    public SmartAiViewModel(
        BatchExecutor executor,
        Action<string, bool> notifyStatus,
        Action refreshUndoState)
    {
        _executor = executor;
        _notifyStatus = notifyStatus;
        _refreshUndoState = refreshUndoState;

        var saved = _settingsStore.Load();
        _endpointUrl = saved.EndpointUrl;
        _modelName = saved.ModelName;
        _apiKey = saved.ApiKey;
        _includeTextSnippets = saved.IncludeTextSnippets;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(userProfile, "Downloads");
        _folderPath = Directory.Exists(downloads) ? downloads : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        SelectDownloadsCommand = new RelayCommand(SelectDownloads);
        GeneratePreviewCommand = new RelayCommand(GeneratePreviewAsync, () => !IsBusy && Directory.Exists(FolderPath) && !string.IsNullOrWhiteSpace(PromptInstruction));
        ApplyCommand = new RelayCommand(ApplyPlan, () => !IsBusy && CurrentPlan is { ReadyCount: > 0 });
    }

    public ObservableCollection<PreviewOperationRow> PreviewRows { get; } = new();

    public RelayCommand SelectDownloadsCommand { get; }
    public RelayCommand GeneratePreviewCommand { get; }
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

    public string PromptInstruction
    {
        get => _promptInstruction;
        set
        {
            if (SetProperty(ref _promptInstruction, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool UseExternalAiEndpoint
    {
        get => _useExternalAiEndpoint;
        set => SetProperty(ref _useExternalAiEndpoint, value);
    }

    public string EndpointUrl
    {
        get => _endpointUrl;
        set => SetProperty(ref _endpointUrl, value);
    }

    public string ModelName
    {
        get => _modelName;
        set => SetProperty(ref _modelName, value);
    }

    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    public bool IncludeTextSnippets
    {
        get => _includeTextSnippets;
        set => SetProperty(ref _includeTextSnippets, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
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

    public string OcrExtractedText
    {
        get => _ocrExtractedText;
        set => SetProperty(ref _ocrExtractedText, value);
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

    public bool CanApply => !IsBusy && CurrentPlan is { ReadyCount: > 0 };

    public void SelectDownloads()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(userProfile, "Downloads");
        FolderPath = Directory.Exists(downloads) ? downloads : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    public async void RunOcrOnImageAsync(string imagePath)
    {
        if (IsBusy || !File.Exists(imagePath))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var text = await WindowsOcrService.ExtractTextAsync(imagePath);
            OcrExtractedText = string.IsNullOrWhiteSpace(text)
                ? "(No readable text detected in image.)"
                : text;
        }
        catch (Exception ex)
        {
            _notifyStatus($"Windows OCR failed: {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void GeneratePreviewAsync()
    {
        if (IsBusy || !Directory.Exists(FolderPath) || string.IsNullOrWhiteSpace(PromptInstruction))
        {
            return;
        }

        IsBusy = true;
        PreviewRows.Clear();
        PreviewSummary = UseExternalAiEndpoint
            ? "Contacting AI endpoint to generate Dry-Run Preview..."
            : "Analyzing filenames & text snippets offline...";

        try
        {
            var config = new AiProviderConfig
            {
                EndpointUrl = EndpointUrl.Trim(),
                ModelName = ModelName.Trim(),
                ApiKey = ApiKey,
                IncludeTextSnippets = IncludeTextSnippets
            };
            _settingsStore.Save(config);

            var files = _scanner.Scan(FolderPath, new FolderScanOptions
            {
                IncludeSubdirectories = false,
                IgnoreActiveDownloads = true,
                SortOrder = FileSortOrder.NameAsc
            });

            var plan = UseExternalAiEndpoint
                ? await _planner.BuildAiPlanAsync(FolderPath, files, PromptInstruction, config)
                : await Task.Run(() => _planner.BuildOfflineSmartPlan(FolderPath, files, PromptInstruction));

            CurrentPlan = plan;
            for (var i = 0; i < plan.Operations.Count; i++)
            {
                PreviewRows.Add(PreviewOperationRow.FromPlanned(i + 1, plan.Operations[i], FolderPath));
            }

            PreviewSummary =
                $"Planned {files.Count} file(s) · Ready: {plan.ReadyCount} · Auto-Numbered: {plan.ConflictResolvedCount} · Skipped: {plan.SkippedCount}";
        }
        catch (Exception ex)
        {
            _notifyStatus($"Smart Plan failed: {ex.Message}", true);
            PreviewSummary = "Failed to generate plan. Check endpoint settings or switch to Offline Smart Sort.";
        }
        finally
        {
            IsBusy = false;
        }
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
            _notifyStatus($"Smart Organize applied {result.AppliedCount} file(s), {result.FailedCount} failed: {string.Join("; ", result.Errors)}", true);
        }
        else
        {
            _notifyStatus($"Smart Organize moved/renamed {result.AppliedCount} file(s). Click Undo Last anytime to restore.", false);
        }

        CurrentPlan = null;
        PreviewRows.Clear();
    }
}
