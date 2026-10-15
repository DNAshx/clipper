using clipper.Models;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace clipper.Services;

public sealed partial class LlmService
{
    private static void ValidateCredentials(LlmSettings settings)
    {
        var profile = settings.ActiveProvider;
        if (string.IsNullOrWhiteSpace(profile.ApiKey))
            throw new LlmException("Добавьте API-ключ в настройках LLM.");
        if (profile.ApiKey.Trim().Any(c => c <= 32 || c > 126))
            throw new LlmException("API-ключ содержит пробелы или недопустимые символы. Вставьте только значение ключа.");
        if (settings.Provider == LlmProvider.Anthropic && profile.WorkspaceId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-'))
            throw new LlmException("Workspace ID содержит недопустимые символы.");
    }

    private static void AddAuthentication(HttpRequestMessage request, LlmSettings settings, bool includeWorkspace = true)
    {
        var profile = settings.ActiveProvider;
        switch (settings.Provider)
        {
            case LlmProvider.OpenAI:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", profile.ApiKey.Trim());
                break;
            case LlmProvider.Anthropic:
                request.Headers.Add("x-api-key", profile.ApiKey.Trim());
                request.Headers.Add("anthropic-version", "2023-06-01");
                if (includeWorkspace && !string.IsNullOrWhiteSpace(profile.WorkspaceId))
                    request.Headers.Add("anthropic-workspace-id", profile.WorkspaceId.Trim());
                break;
            case LlmProvider.Gemini:
                request.Headers.Add("x-goog-api-key", profile.ApiKey.Trim());
                break;
        }
    }

    private async Task<JsonDocument> SendJsonAsync(LlmSettings settings,
        Func<HttpRequestMessage> createRequest, CancellationToken cancellationToken)
    {
        ValidateCredentials(settings);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var request = createRequest();
                using var response = await client.SendAsync(request, timeout.Token);
                if (response.IsSuccessStatusCode)
                    return JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                var error = await ReadApiErrorAsync(response, settings.Provider, timeout.Token);
                if (attempt == 0 && error.Code == "workspace_required" && string.IsNullOrWhiteSpace(settings.ActiveProvider.WorkspaceId))
                {
                    // Organization-wide keys must explicitly select a workspace. Resolve only
                    // when the account has exactly one, never choose between multiple scopes.
                    settings.ActiveProvider.WorkspaceId = await ResolveWorkspaceAsync(settings, timeout.Token);
                    continue;
                }
                throw error;
            }
            throw new LlmException("Не удалось подключиться к LLM.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new LlmException("LLM не ответила за 25 секунд. Повторите позже."); }
        catch (HttpRequestException)
        { throw new LlmException("Не удалось подключиться к LLM. Проверьте интернет."); }
        catch (JsonException)
        { throw new LlmException("Сервис LLM вернул некорректный ответ."); }
    }

    private async Task<string> ResolveWorkspaceAsync(LlmSettings settings, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.anthropic.com/v1/organizations/workspaces?include_default=true&limit=100");
        AddAuthentication(request, settings, includeWorkspace: false);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new LlmException("Ключ Claude требует Workspace ID. Укажите его в настройках Anthropic или используйте ключ конкретного workspace.", "workspace_required");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new LlmException("Не удалось определить workspace Claude. Укажите Workspace ID вручную.", "workspace_required");
        var ids = data.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object)
            .Where(item => !item.TryGetProperty("archived_at", out var archived)
                || archived.ValueKind == JsonValueKind.Null)
            .Select(item => GetString(item, "id")).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
        bool more = root.TryGetProperty("has_more", out var hasMore) && hasMore.ValueKind == JsonValueKind.True;
        if (ids.Length != 1 || more)
            throw new LlmException("Ключ Claude требует выбора workspace. Укажите Workspace ID в настройках Anthropic.", "workspace_required");
        return ids[0]!;
    }

    private static async Task<LlmException> ReadApiErrorAsync(HttpResponseMessage response, LlmProvider provider, CancellationToken cancellationToken)
    {
        // Classify provider errors without exposing raw messages, credentials, or request bodies.
        string message = "";
        string type = "";
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object)
            {
                message = GetString(error, "message") ?? "";
                type = GetString(error, "type") ?? GetString(error, "status") ?? "";
            }
        }
        catch (JsonException) { }
        if (provider == LlmProvider.Anthropic && response.StatusCode == HttpStatusCode.BadRequest
            && message.Contains("anthropic-workspace-id", StringComparison.OrdinalIgnoreCase))
            return new("Ключ Claude требует Workspace ID. Укажите его в настройках Anthropic.", "workspace_required");
        if ((response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.PaymentRequired
                || response.StatusCode == HttpStatusCode.TooManyRequests)
            && (message.Contains("credit balance", StringComparison.OrdinalIgnoreCase)
                || message.Contains("billing", StringComparison.OrdinalIgnoreCase)
                || type == "insufficient_quota"))
            return new("Недостаточно средств или квоты API. Проверьте баланс API и биллинг провайдера; подписка на чат не заменяет API-кредиты.", "billing");
        return new(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "API-ключ не принят. Проверьте значение ключа и его срок действия.",
            HttpStatusCode.Forbidden => "Доступ API запрещён. Проверьте права ключа, workspace и доступ к модели.",
            HttpStatusCode.TooManyRequests => "Лимит API: проверьте квоту или повторите позже.",
            HttpStatusCode.BadRequest or HttpStatusCode.NotFound => "Модель или запрос недоступны. Обновите список моделей и проверьте выбранный ID.",
            _ => $"Сервис LLM временно недоступен (HTTP {(int)response.StatusCode})."
        });
    }

    private static string? GetString(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
