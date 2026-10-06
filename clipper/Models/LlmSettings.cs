using clipper.Data;

namespace clipper.Models;

public enum LlmProvider { OpenAI, Anthropic, Gemini }
public enum SpeechEvent { Greeting, SaySomething, Poked, WakeUp, AppChanged, CurrentWindow }

public sealed class ProviderSettings
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
}

public sealed class LlmSettings
{
    public LlmProvider Provider { get; set; } = LlmProvider.OpenAI;
    public Dictionary<LlmProvider, ProviderSettings> Providers { get; set; } =
        Enum.GetValues<LlmProvider>().ToDictionary(p => p, p => new ProviderSettings
        {
            Model = ModelCatalog.Recommended(p)
        });
    public string Language { get; set; } = "Russian";
    public bool ShareWindowContext { get; set; }
    public bool AutomaticReactions { get; set; }
    public string SystemPrompt { get; set; } = PromptLibrary.SystemPrompt;
    public Dictionary<SpeechEvent, string> Prompts { get; set; } = new(PromptLibrary.Events);

    public ProviderSettings ActiveProvider => Providers[Provider];
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ActiveProvider.ApiKey)
        && !string.IsNullOrWhiteSpace(ActiveProvider.Model);

    public void Normalize()
    {
        if (!Enum.IsDefined(Provider)) Provider = LlmProvider.OpenAI;
        Providers ??= new();
        foreach (var provider in Enum.GetValues<LlmProvider>())
        {
            if (!Providers.TryGetValue(provider, out var value) || value is null)
                Providers[provider] = value = new();
            value.ApiKey ??= "";
            if (string.IsNullOrWhiteSpace(value.Model)) value.Model = ModelCatalog.Recommended(provider);
        }
        if (string.IsNullOrWhiteSpace(Language)) Language = "Russian";
        if (string.IsNullOrWhiteSpace(SystemPrompt)) SystemPrompt = PromptLibrary.SystemPrompt;
        Prompts ??= new();
        foreach (var entry in PromptLibrary.Events)
            if (!Prompts.TryGetValue(entry.Key, out var value) || string.IsNullOrWhiteSpace(value))
                Prompts[entry.Key] = entry.Value;
    }
}
