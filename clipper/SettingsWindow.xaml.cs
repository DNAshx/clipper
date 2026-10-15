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
    private bool windowLoaded;
    private CancellationTokenSource? modelRefresh;
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
        Loaded += (_, _) =>
        {
            windowLoaded = true;
            ScheduleModelRefresh(immediate: true);
        };
        Closed += (_, _) =>
        {
            lifetime.Cancel();
            modelRefresh?.Cancel();
        };
    }

    private void CaptureProvider()
    {
        if (displayedProvider is not { } provider) return;
        draft.Providers[provider].ApiKey = KeyBox.Password.Trim();
        draft.Providers[provider].Model = ModelBox.Text.Trim();
        if (provider == LlmProvider.Anthropic) draft.Providers[provider].WorkspaceId = WorkspaceBox.Text.Trim();
    }

    private void Provider_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ProviderBox.SelectedItem is not LlmProvider provider) return;
        CaptureProvider();
        displayedProvider = provider;
        draft.Provider = provider;
        loadingProvider = true;
        KeyBox.Password = draft.Providers[provider].ApiKey;
        WorkspacePanel.Visibility = provider == LlmProvider.Anthropic ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceBox.Text = draft.Providers[provider].WorkspaceId;
        ModelBox.ItemsSource = ModelCatalog.For(provider);
        ModelBox.Text = draft.Providers[provider].Model;
        RecommendationText.Text = $"Рекомендуется {ModelCatalog.Recommended(provider)} — компактная модель для быстрых коротких реплик.";
        loadingProvider = false;
        StatusText.Text = "";
        ScheduleModelRefresh(immediate: true);
    }

    private void Key_Changed(object sender, RoutedEventArgs e)
    {
        if (loadingProvider || StatusText == null) return;
        StatusText.Text = "";
        ScheduleModelRefresh();
    }

    private void Workspace_Changed(object sender, TextChangedEventArgs e)
    {
        if (!loadingProvider && ModelsStatusText != null) ScheduleModelRefresh();
    }

    private void RefreshModels_Click(object sender, RoutedEventArgs e) => ScheduleModelRefresh(immediate: true);

    private void UseBuiltInModels()
    {
        string selected = ModelBox.Text;
        ModelBox.ItemsSource = ModelCatalog.For(draft.Provider);
        ModelBox.Text = selected;
    }

    private async void ScheduleModelRefresh(bool immediate = false)
    {
        modelRefresh?.Cancel();
        if (!windowLoaded || loadingProvider) return;
        var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        modelRefresh = request;
        UseBuiltInModels();
        ModelsStatusText.Text = "Встроенный список моделей. Подключите ключ для загрузки из API.";
        RefreshModelsButton.IsEnabled = !string.IsNullOrWhiteSpace(KeyBox.Password);
        try
        {
            if (string.IsNullOrWhiteSpace(KeyBox.Password)) return;
            if (!immediate) await Task.Delay(700, request.Token);
            await RefreshModelsAsync(request.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (modelRefresh == request) modelRefresh = null;
            request.Dispose();
        }
    }

    private async Task RefreshModelsAsync(CancellationToken cancellationToken)
    {
        CaptureProvider();
        var snapshot = SettingsStore.Copy(draft);
        ModelsStatusText.Text = "Загрузка моделей…";
        try
        {
            var models = await llm.ListModelsAsync(snapshot, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            // Key/provider edits cancel this operation. Never apply their older response.
            if (snapshot.Provider != draft.Provider || snapshot.ActiveProvider.ApiKey != KeyBox.Password.Trim()
                || (snapshot.Provider == LlmProvider.Anthropic && draft.ActiveProvider.WorkspaceId != WorkspaceBox.Text.Trim())) return;
            string selected = ModelBox.Text;
            ModelBox.ItemsSource = ModelCatalog.For(draft.Provider).Concat(models).Distinct(StringComparer.Ordinal).ToArray();
            ModelBox.Text = selected;
            if (snapshot.Provider == LlmProvider.Anthropic && string.IsNullOrWhiteSpace(WorkspaceBox.Text))
            {
                loadingProvider = true;
                WorkspaceBox.Text = snapshot.ActiveProvider.WorkspaceId;
                draft.ActiveProvider.WorkspaceId = snapshot.ActiveProvider.WorkspaceId;
                loadingProvider = false;
            }
            ModelsStatusText.Text = models.Length > 0
                ? $"Из API загружено моделей: {models.Length}. Встроенный список сохранён."
                : "API не вернул подходящих текстовых моделей. Используется встроенный список.";
        }
        catch (LlmException ex)
        {
            if (!cancellationToken.IsCancellationRequested)
                ModelsStatusText.Text = ex.Message + " Используется встроенный список моделей.";
        }
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
        if (draft.ActiveProvider.ApiKey.Any(c => c <= 32 || c > 126))
        {
            StatusText.Text = "API-ключ содержит недопустимые символы.";
            return false;
        }
        if (draft.Provider == LlmProvider.Anthropic
            && draft.ActiveProvider.WorkspaceId.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-'))
        {
            StatusText.Text = "Workspace ID содержит недопустимые символы.";
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
            if (snapshot.Provider == draft.Provider && snapshot.ActiveProvider.ApiKey == KeyBox.Password.Trim())
            {
                StatusText.Text = "Подключение работает: " + text;
                if (snapshot.Provider == LlmProvider.Anthropic && string.IsNullOrWhiteSpace(WorkspaceBox.Text))
                {
                    loadingProvider = true;
                    WorkspaceBox.Text = snapshot.ActiveProvider.WorkspaceId;
                    loadingProvider = false;
                    draft.ActiveProvider.WorkspaceId = snapshot.ActiveProvider.WorkspaceId;
                }
                ScheduleModelRefresh(immediate: true);
            }
        }
        catch (LlmException ex)
        {
            if (snapshot.Provider == draft.Provider && snapshot.ActiveProvider.ApiKey == KeyBox.Password.Trim())
                StatusText.Text = ex.Message;
        }
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
