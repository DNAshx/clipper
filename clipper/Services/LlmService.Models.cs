using clipper.Models;
using System.Net.Http;
using System.Text.Json;

namespace clipper.Services;

public sealed partial class LlmService
{
    public async Task<string[]> ListModelsAsync(LlmSettings settings, CancellationToken cancellationToken = default)
    {
        ValidateCredentials(settings);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        try
        {
            for (int page = 0; page < 100; page++)
            {
                string url = settings.Provider switch
                {
                    LlmProvider.OpenAI => "https://api.openai.com/v1/models",
                    LlmProvider.Anthropic => "https://api.anthropic.com/v1/models?limit=100"
                        + (cursor == null ? "" : "&after_id=" + Uri.EscapeDataString(cursor)),
                    LlmProvider.Gemini => "https://generativelanguage.googleapis.com/v1beta/models?pageSize=1000"
                        + (cursor == null ? "" : "&pageToken=" + Uri.EscapeDataString(cursor)),
                    _ => throw new LlmException("Выберите провайдера LLM.")
                };
                using var json = await SendJsonAsync(settings, () =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    AddAuthentication(request, settings);
                    return request;
                }, timeout.Token);
                var root = json.RootElement;
                string property = settings.Provider == LlmProvider.Gemini ? "models" : "data";
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(property, out var models)
                    || models.ValueKind != JsonValueKind.Array)
                    throw new LlmException("Сервис вернул неожиданный формат списка моделей.");
                foreach (var model in models.EnumerateArray())
                {
                    string? id = GetString(model, settings.Provider == LlmProvider.Gemini ? "name" : "id");
                    if (id == null) continue;
                    if (id.StartsWith("models/", StringComparison.Ordinal)) id = id[7..];
                    if (settings.Provider == LlmProvider.Gemini
                        && (!model.TryGetProperty("supportedGenerationMethods", out var methods)
                            || methods.ValueKind != JsonValueKind.Array
                            || !methods.EnumerateArray().Any(method => method.ValueKind == JsonValueKind.String && method.GetString() == "generateContent")))
                        continue;
                    if (IsSpeechModel(settings.Provider, id)) ids.Add(id);
                }
                cursor = null;
                if (settings.Provider == LlmProvider.Anthropic && root.TryGetProperty("has_more", out var more)
                    && more.ValueKind == JsonValueKind.True)
                {
                    cursor = GetString(root, "last_id");
                    if (string.IsNullOrWhiteSpace(cursor)) throw new LlmException("Сервис вернул некорректную страницу моделей.");
                }
                else if (settings.Provider == LlmProvider.Gemini) cursor = GetString(root, "nextPageToken");
                if (string.IsNullOrWhiteSpace(cursor)) return ids.Order(StringComparer.Ordinal).ToArray();
                if (!cursors.Add(cursor)) throw new LlmException("Сервис повторяет одну и ту же страницу моделей.");
            }
            throw new LlmException("Слишком много страниц моделей. Повторите загрузку позже.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new LlmException("Список моделей не загрузился за 25 секунд."); }
    }

    private static bool IsSpeechModel(LlmProvider provider, string id)
    {
        bool family = provider switch
        {
            LlmProvider.OpenAI => id.StartsWith("gpt-", StringComparison.Ordinal)
                || id.StartsWith("chatgpt-", StringComparison.Ordinal)
                || (id.Length > 1 && id[0] == 'o' && char.IsAsciiDigit(id[1])),
            LlmProvider.Anthropic => id.StartsWith("claude-", StringComparison.Ordinal),
            LlmProvider.Gemini => id.StartsWith("gemini-", StringComparison.Ordinal),
            _ => false
        };
        return family && !new[] { "audio", "realtime", "transcri", "-tts", "-image", "-live", "embedding" }
            .Any(part => id.Contains(part, StringComparison.OrdinalIgnoreCase));
    }
}
