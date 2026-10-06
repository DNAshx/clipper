using clipper.Data;
using clipper.Models;
using clipper.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace clipper;

public partial class SettingsWindow : Window
{
    private readonly LlmSettings draft;
    private readonly SettingsStore store;
    private readonly LlmService llm;
    private readonly Dictionary<SpeechEvent, TextBox> promptBoxes = new();
    private LlmProvider? displayedProvider;
    private bool loadingProvider;
    private readonly CancellationTokenSource lifetime = new();

    public LlmSettings? SavedSettings { get; private set; }

    public SettingsWindow(LlmSettings settings, SettingsStore store, LlmService llm)
    {
        this.store = store;
        this.llm = llm;
        draft = SettingsStore.Copy(settings);
        InitializeComponent();
        foreach (var speechEvent in Enum.GetValues<SpeechEvent>())
        {
            EventPromptPanel.Children.Add(new TextBlock
            {
                Text = PromptLibrary.Label(speechEvent), FontWeight = FontWeights.SemiBold
            });
            var box = new TextBox
            {
                AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 90,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = 4000
            };
            promptBoxes[speechEvent] = box;
            EventPromptPanel.Children.Add(box);
        }
        FillPrompts();
        LanguageBox.Text = draft.Language;
        ContextBox.IsChecked = draft.ShareWindowContext;
        AutomaticBox.IsChecked = draft.AutomaticReactions;
        ProviderBox.ItemsSource = Enum.GetValues<LlmProvider>();
        ProviderBox.SelectedItem = draft.Provider;
        Closed += (_, _) => lifetime.Cancel();
    }

    private void CaptureProvider()
    {
        if (displayedProvider is not { } provider) return;
        draft.Providers[provider].ApiKey = KeyBox.Password.Trim();
        draft.Providers[provider].Model = ModelBox.Text.Trim();
    }

    private void Provider_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ProviderBox.SelectedItem is not LlmProvider provider) return;
        CaptureProvider();
        displayedProvider = provider;
        draft.Provider = provider;
        loadingProvider = true;
        KeyBox.Password = draft.Providers[provider].ApiKey;
        ModelBox.ItemsSource = ModelCatalog.For(provider);
        ModelBox.Text = draft.Providers[provider].Model;
        RecommendationText.Text = $"Рекомендуется {ModelCatalog.Recommended(provider)} — компактная модель для быстрых коротких реплик.";
        loadingProvider = false;
        StatusText.Text = "";
    }

    private void Key_Changed(object sender, RoutedEventArgs e)
    {
        if (!loadingProvider && StatusText != null) StatusText.Text = "";
    }

    private void Recommended_Click(object sender, RoutedEventArgs e) =>
        ModelBox.Text = ModelCatalog.Recommended(draft.Provider);

    private void RemoveKey_Click(object sender, RoutedEventArgs e)
    {
        KeyBox.Clear();
        StatusText.Text = "Ключ будет удалён после сохранения настроек.";
    }

    private void OpenKeyPage_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(ModelCatalog.KeyUrl(draft.Provider)) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { StatusText.Text = "Не удалось открыть браузер."; }
    }

    private void FillPrompts()
    {
        SystemPromptBox.Text = draft.SystemPrompt;
        foreach (var entry in promptBoxes) entry.Value.Text = draft.Prompts[entry.Key];
    }

    private void ResetPrompts_Click(object sender, RoutedEventArgs e)
    {
        draft.SystemPrompt = PromptLibrary.SystemPrompt;
        draft.Prompts = new(PromptLibrary.Events);
        FillPrompts();
    }

    private bool ReadDraft()
    {
        CaptureProvider();
        draft.Language = LanguageBox.Text.Trim();
        draft.ShareWindowContext = ContextBox.IsChecked == true;
        draft.AutomaticReactions = AutomaticBox.IsChecked == true;
        draft.SystemPrompt = SystemPromptBox.Text.Trim();
        foreach (var entry in promptBoxes) draft.Prompts[entry.Key] = entry.Value.Text.Trim();
        if (string.IsNullOrWhiteSpace(draft.ActiveProvider.Model) || string.IsNullOrWhiteSpace(draft.Language)
            || string.IsNullOrWhiteSpace(draft.SystemPrompt) || draft.Prompts.Values.Any(string.IsNullOrWhiteSpace))
        {
            StatusText.Text = "Укажите модель, язык и заполните все промпты.";
            return false;
        }
        if (draft.ActiveProvider.ApiKey.Any(char.IsControl))
        {
            StatusText.Text = "API-ключ содержит недопустимые символы.";
            return false;
        }
        return true;
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (!ReadDraft()) return;
        TestButton.IsEnabled = false;
        StatusText.Text = "Проверка подключения…";
        var snapshot = SettingsStore.Copy(draft);
        try
        {
            var prompt = PromptLibrary.Build(snapshot, SpeechEvent.Greeting);
            string text = await llm.GenerateAsync(snapshot, prompt.System, prompt.User, lifetime.Token);
            StatusText.Text = "Подключение работает: " + text;
        }
        catch (LlmException ex) { StatusText.Text = ex.Message; }
        catch (OperationCanceledException) { }
        finally { TestButton.IsEnabled = true; }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!ReadDraft()) return;
        try
        {
            store.Save(draft);
            SavedSettings = draft;
            DialogResult = true;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        { StatusText.Text = "Не удалось сохранить настройки. Проверьте доступ к папке LocalAppData."; }
    }
}
