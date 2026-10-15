using clipper.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace clipper.Services;

public sealed class LlmException(string message, string? code = null) : Exception(message)
{
    public string? Code { get; } = code;
}

public sealed partial class LlmService(HttpClient client)
{
    public async Task<string> GenerateAsync(LlmSettings settings, string system, string user,
        CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured)
            throw new LlmException("Добавьте API-ключ в настройках LLM.");

        try
        {
            using var json = await SendJsonAsync(settings, () => CreateRequest(settings, system, user), cancellationToken);
            string text = ParseText(settings.Provider, json.RootElement);
            if (string.IsNullOrWhiteSpace(text))
                throw new LlmException("Модель не вернула текст. Попробуйте другую модель или промпт.");
            text = Regex.Replace(text, @"\s+", " ").Trim().Trim('"', '«', '»');
            if (text.Length > 180)
            {
                int end = char.IsHighSurrogate(text[176]) ? 176 : 177;
                text = text[..end].TrimEnd() + "…";
            }
            return text;
        }
        catch (JsonException)
        { throw new LlmException("Сервис LLM вернул некорректный ответ."); }
        catch (InvalidOperationException)
        { throw new LlmException("Сервис LLM вернул неожиданный формат ответа."); }
    }

    private static HttpRequestMessage CreateRequest(LlmSettings settings, string system, string user)
    {
        var provider = settings.ActiveProvider;
        HttpRequestMessage request;
        switch (settings.Provider)
        {
            case LlmProvider.OpenAI:
                var body = new Dictionary<string, object>
                {
                    ["model"] = provider.Model, ["instructions"] = system, ["input"] = user,
                    ["max_output_tokens"] = provider.Model == "gpt-6-luna" || provider.Model == "gpt-6-sol" ? 256 : 1024,
                    ["store"] = false
                };
                if (provider.Model == "gpt-6-luna" || provider.Model == "gpt-6-sol")
                    body["reasoning"] = new { effort = "none" };
                else if (provider.Model == "gpt-6-astra")
                    body["reasoning"] = new { effort = "low" };
                request = new(HttpMethod.Post, "https://api.openai.com/v1/responses") { Content = JsonContent.Create(body) };
                break;
            case LlmProvider.Anthropic:
                request = new(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
                {
                    Content = JsonContent.Create(new { model = provider.Model, system, max_tokens = 256,
                        messages = new[] { new { role = "user", content = user } } })
                };
                break;
            case LlmProvider.Gemini:
                var config = new Dictionary<string, object>
                {
                    ["maxOutputTokens"] = provider.Model == "gemini-3.5-flash-lite" ? 256 : 1024
                };
                if (provider.Model == "gemini-3.5-flash-lite")
                    config["thinkingConfig"] = new { thinkingLevel = "MINIMAL" };
                else if (provider.Model == "gemini-3.8-flash" || provider.Model == "gemini-3.1-pro-preview")
                    config["thinkingConfig"] = new { thinkingLevel = "LOW" };
                request = new(HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/models/"
                    + Uri.EscapeDataString(provider.Model) + ":generateContent")
                {
                    Content = JsonContent.Create(new
                    {
                        systemInstruction = new { parts = new[] { new { text = system } } },
                        contents = new[] { new { role = "user", parts = new[] { new { text = user } } } },
                        generationConfig = config
                    })
                };
                break;
            default: throw new LlmException("Выберите провайдера LLM в настройках.");
        }
        AddAuthentication(request, settings);
        return request;
    }

    private static string ParseText(LlmProvider provider, JsonElement root)
    {
        IEnumerable<JsonElement> parts = [];
        if (provider == LlmProvider.OpenAI && root.TryGetProperty("output", out var output))
            parts = output.EnumerateArray().Where(item => IsType(item, "message"))
                .Where(item => item.TryGetProperty("content", out _))
                .SelectMany(item => item.GetProperty("content").EnumerateArray())
                .Where(item => IsType(item, "output_text"));
        else if (provider == LlmProvider.Anthropic && root.TryGetProperty("content", out var content))
            parts = content.EnumerateArray().Where(item => IsType(item, "text"));
        else if (provider == LlmProvider.Gemini && root.TryGetProperty("candidates", out var candidates)
            && candidates.GetArrayLength() > 0 && candidates[0].TryGetProperty("content", out var candidate)
            && candidate.TryGetProperty("parts", out var geminiParts))
            parts = geminiParts.EnumerateArray().Where(item => !item.TryGetProperty("thought", out var thought)
                || thought.ValueKind != JsonValueKind.True);
        return string.Join(" ", parts.Where(item => item.TryGetProperty("text", out var text)
            && text.ValueKind == JsonValueKind.String).Select(item => item.GetProperty("text").GetString()));
    }

    private static bool IsType(JsonElement item, string type) => item.TryGetProperty("type", out var value)
        && value.ValueKind == JsonValueKind.String && value.GetString() == type;
}
