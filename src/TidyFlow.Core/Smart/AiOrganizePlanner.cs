using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TidyFlow.Core.Models;
using TidyFlow.Core.Organizing;
using TidyFlow.Core.Renaming;
using TidyFlow.Core.Safety;

namespace TidyFlow.Core.Smart;

/// <summary>
/// Builds dry-run OperationPlans from plain-English organization prompts using an OpenAI-compatible endpoint
/// (local Ollama, LM Studio, or cloud API) with an offline heuristic fallback.
/// </summary>
public sealed class AiOrganizePlanner
{
    private static readonly HashSet<string> TextReadableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".csv", ".xml", ".yaml", ".yml", ".log", ".ini", ".cfg"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ConflictResolver _conflictResolver = new();
    private readonly HttpClient _httpClient;

    public AiOrganizePlanner(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
    }

    public static IReadOnlyList<SmartFileContext> BuildFileContexts(
        IReadOnlyList<FileEntry> files,
        bool includeTextSnippets = true,
        int maxFiles = 120,
        int maxSnippetChars = 320)
    {
        var list = new List<SmartFileContext>(Math.Min(files.Count, maxFiles));

        for (var i = 0; i < files.Count && i < maxFiles; i++)
        {
            var file = files[i];
            string? snippet = null;

            if (includeTextSnippets && TextReadableExtensions.Contains(file.Extension))
            {
                snippet = TryReadSnippet(file.FullPath, maxSnippetChars);
            }

            list.Add(new SmartFileContext(i, file.FullPath, file.Name, file.Extension, file.SizeBytes, snippet));
        }

        return list;
    }

    public async Task<OperationPlan> BuildAiPlanAsync(
        string sourceRootDirectory,
        IReadOnlyList<FileEntry> files,
        string userInstruction,
        AiProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userInstruction))
        {
            throw new ArgumentException("Instruction prompt must not be empty.", nameof(userInstruction));
        }

        var contexts = BuildFileContexts(files, config.IncludeTextSnippets);
        var promptBody = BuildPromptPayload(contexts, userInstruction);

        var requestPayload = new
        {
            model = config.ModelName,
            temperature = 0.1,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You are a Windows file organizer. Respond ONLY with a valid JSON array of objects: " +
                              "[{\"index\": 0, \"folder\": \"FolderName\", \"newName\": null, \"reason\": \"short reason\"}]. " +
                              "Do not include file extensions in newName. Keep folder names concise and valid for Windows."
                },
                new
                {
                    role = "user",
                    content = promptBody
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, config.EndpointUrl);
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey.Trim());
        }

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestPayload),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"AI endpoint returned {(int)response.StatusCode}: {rawResponse}");
        }

        var assistantText = ExtractAssistantMessageContent(rawResponse);
        var suggestions = ParseSuggestions(assistantText);

        return MaterializePlan(sourceRootDirectory, files, suggestions, $"AI Organize: {userInstruction}");
    }

    public OperationPlan BuildOfflineSmartPlan(
        string sourceRootDirectory,
        IReadOnlyList<FileEntry> files,
        string userInstruction)
    {
        var tokens = (userInstruction ?? string.Empty)
            .Split(new[] { ' ', ',', '.', ';', ':', '/', '\\', '\n', '\r', '\t', '"', '\'' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var suggestions = new List<AiSuggestedAction>(files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var snippet = TextReadableExtensions.Contains(file.Extension)
                ? TryReadSnippet(file.FullPath, 400)
                : null;

            string? matchedToken = null;
            foreach (var token in tokens)
            {
                if (file.NameWithoutExtension.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(snippet) && snippet.Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    matchedToken = char.ToUpperInvariant(token[0]) + token[1..];
                    break;
                }
            }

            var folder = matchedToken ?? FileCategoryRegistry.GetCategory(file.Extension);
            var reason = matchedToken is not null
                ? $"Matched prompt keyword '{matchedToken}'"
                : $"Category fallback ({folder})";

            suggestions.Add(new AiSuggestedAction(i, folder, null, reason));
        }

        return MaterializePlan(sourceRootDirectory, files, suggestions, $"Offline Smart Organize ({files.Count} file(s))");
    }

    public OperationPlan MaterializePlan(
        string sourceRootDirectory,
        IReadOnlyList<FileEntry> files,
        IReadOnlyList<AiSuggestedAction> suggestions,
        string summary)
    {
        var byIndex = suggestions
            .GroupBy(s => s.Index)
            .ToDictionary(g => g.Key, g => g.First());

        var proposals = new List<ProposedOperation>(files.Count);

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (!byIndex.TryGetValue(i, out var action) || string.IsNullOrWhiteSpace(action.TargetFolder))
            {
                proposals.Add(new ProposedOperation(
                    file.FullPath,
                    file.FullPath,
                    OperationKind.Move,
                    InitialNote: "Unchanged (no suggestion).",
                    ForceSkip: true));
                continue;
            }

            var folderSegments = action.TargetFolder
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s is not "." and not "..")
                .Select(s => FilenameSanitizer.SanitizeName(s, "_"))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            if (folderSegments.Length == 0)
            {
                proposals.Add(new ProposedOperation(
                    file.FullPath,
                    file.FullPath,
                    OperationKind.Move,
                    InitialNote: "Invalid target folder suggested.",
                    ForceSkip: true));
                continue;
            }

            var destDir = Path.Combine(new[] { sourceRootDirectory }.Concat(folderSegments).ToArray());

            var destFileName = file.Name;
            var opKind = OperationKind.Move;

            if (!string.IsNullOrWhiteSpace(action.SuggestedNameWithoutExtension))
            {
                var sanitizedStem = FilenameSanitizer.SanitizeName(action.SuggestedNameWithoutExtension, "_");
                if (!string.IsNullOrWhiteSpace(sanitizedStem))
                {
                    destFileName = $"{sanitizedStem}{file.Extension}";
                    if (!string.Equals(destFileName, file.Name, StringComparison.Ordinal))
                    {
                        opKind = OperationKind.MoveAndRename;
                    }
                }
            }

            var destPath = Path.Combine(destDir, destFileName);
            var note = string.IsNullOrWhiteSpace(action.Reason)
                ? $"-> {Path.Combine(folderSegments)}"
                : action.Reason;

            proposals.Add(new ProposedOperation(file.FullPath, destPath, opKind, note));
        }

        return _conflictResolver.Resolve(proposals, summary);
    }

    public static IReadOnlyList<AiSuggestedAction> ParseSuggestions(string rawAssistantText)
    {
        if (string.IsNullOrWhiteSpace(rawAssistantText))
        {
            return Array.Empty<AiSuggestedAction>();
        }

        var trimmed = rawAssistantText.Trim();

        // Strip markdown ```json ... ``` fences if present
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0)
            {
                trimmed = trimmed[..lastFence];
            }

            trimmed = trimmed.Trim();
        }

        var arrayStart = trimmed.IndexOf('[');
        var arrayEnd = trimmed.LastIndexOf(']');
        if (arrayStart >= 0 && arrayEnd > arrayStart)
        {
            trimmed = trimmed[arrayStart..(arrayEnd + 1)];
        }

        var items = JsonSerializer.Deserialize<List<RawAiSuggestionDto>>(trimmed, JsonOptions);
        if (items is null)
        {
            return Array.Empty<AiSuggestedAction>();
        }

        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.Folder))
            .Select(item => new AiSuggestedAction(item.Index, item.Folder!, item.NewName, item.Reason))
            .ToList();
    }

    private static string ExtractAssistantMessageContent(string rawApiJson)
    {
        using var doc = JsonDocument.Parse(rawApiJson);
        if (doc.RootElement.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? string.Empty;
            }
        }

        // Fallback if raw JSON array was returned directly
        return rawApiJson;
    }

    private static string BuildPromptPayload(IReadOnlyList<SmartFileContext> contexts, string userInstruction)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"User instruction: {userInstruction}");
        sb.AppendLine("Files:");
        foreach (var ctx in contexts)
        {
            sb.Append($"[{ctx.Index}] {ctx.FileName} ({ctx.SizeBytes} bytes)");
            if (!string.IsNullOrWhiteSpace(ctx.TextSnippet))
            {
                sb.Append($" | snippet: \"{ctx.TextSnippet.Replace("\r", " ").Replace("\n", " ")}\"");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string? TryReadSnippet(string path, int maxChars)
    {
        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var buffer = new char[maxChars];
            var read = reader.Read(buffer, 0, maxChars);
            return read > 0 ? new string(buffer, 0, read) : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class RawAiSuggestionDto
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("folder")]
        public string? Folder { get; set; }

        [JsonPropertyName("newName")]
        public string? NewName { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
}
