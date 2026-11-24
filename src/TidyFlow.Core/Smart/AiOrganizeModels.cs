namespace TidyFlow.Core.Smart;

public sealed record AiProviderConfig
{
    public string EndpointUrl { get; init; } = "http://localhost:11434/v1/chat/completions";

    public string ModelName { get; init; } = "llama3.2";

    public string ApiKey { get; init; } = string.Empty;

    public bool IncludeTextSnippets { get; init; } = true;
}

public sealed record SmartFileContext(
    int Index,
    string FullPath,
    string FileName,
    string Extension,
    long SizeBytes,
    string? TextSnippet);

public sealed record AiSuggestedAction(
    int Index,
    string TargetFolder,
    string? SuggestedNameWithoutExtension = null,
    string? Reason = null);
