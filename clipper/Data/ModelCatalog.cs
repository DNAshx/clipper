using clipper.Models;

namespace clipper.Data;

public static class ModelCatalog
{
    // Provider catalogs change; the settings model field also accepts a custom API ID.
    public static string[] For(LlmProvider provider) => provider switch
    {
        LlmProvider.OpenAI => ["gpt-6-luna", "gpt-6-sol", "gpt-6-astra"],
        LlmProvider.Anthropic => ["claude-haiku-4-5", "claude-sonnet-5-5", "claude-opus-5-5"],
        LlmProvider.Gemini => ["gemini-3.5-flash-lite", "gemini-3.8-flash", "gemini-3.1-pro-preview"],
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    public static string Recommended(LlmProvider provider) => For(provider)[0];

    public static string KeyUrl(LlmProvider provider) => provider switch
    {
        LlmProvider.OpenAI => "https://platform.openai.com/api-keys",
        LlmProvider.Anthropic => "https://platform.claude.com/settings/keys",
        LlmProvider.Gemini => "https://aistudio.google.com/api-keys",
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };
}
