using clipper.Data;
using clipper.Models;
using clipper.Services;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Separator = System.Windows.Controls.Separator;
using Point = System.Windows.Point;

namespace clipper;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer appWatcherTimer;
    private readonly DispatcherTimer speechTimer;
    private readonly ArchetypeService archetypeService;
    private readonly CharacterSpriteService spriteService;
    private readonly CursorTrackingService cursorTrackingService;
    private readonly ClippyStateService stateService = new();
    private readonly TrayService trayService;
    private readonly BlinkService blinkService;
    private readonly SleepService sleepService = new();
    private readonly ReactionCooldownService cooldownService = new();
    private readonly SettingsStore settingsStore;
    private readonly HttpClient httpClient;
    private readonly LlmService llm;
    private LlmSettings settings;
    private SettingsWindow? settingsWindow;
    private ActiveWindowInfo? lastExternalWindow;
    private string lastWindowSignature = "";
    private readonly Queue<string> recentLines = new();
    private CancellationTokenSource? speechRequest;
    private bool speechRequestAutomatic;
    private DateTime lastRequestAt = DateTime.MinValue;
    private DateTime lastAutomaticAt = DateTime.MinValue;
    private Point? pressPoint;
    private bool dragging;
    private bool reallyExit;

    public MainWindow() : this(new SettingsStore(), new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })) { }

    public MainWindow(SettingsStore settingsStore, HttpClient httpClient)
    {
        this.settingsStore = settingsStore;
        this.httpClient = httpClient;
        InitializeComponent();
        settings = settingsStore.Load();
        archetypeService = new ArchetypeService(settings.ArchetypeId);
        llm = new LlmService(httpClient);
        trayService = new TrayService();
        trayService.ShowRequested += ShowFromTray;
        trayService.SettingsRequested += OpenSettings;
        trayService.ExitRequested += ExitFromTray;
        sleepService.IdleReached += OnClippyFellAsleep;
        sleepService.ActivityDetected += OnUserActivityDetected;
        spriteService = new CharacterSpriteService(CharacterCanvas, EyesCanvas, ClippyImage);
        spriteService.SetSprites(archetypeService.Current.Sprites);
        blinkService = new BlinkService(spriteService.SetBlinking);
        cursorTrackingService = new CursorTrackingService(CharacterCanvas, spriteService);
        appWatcherTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        appWatcherTimer.Tick += AppWatcherTimer_Tick;
        speechTimer = new DispatcherTimer { Interval = AnimationService.SpeechAnimationDuration };
        speechTimer.Tick += (_, _) =>
        {
            speechTimer.Stop();
            if (stateService.CurrentState == ClippyState.Speaking) stateService.SetIdle();
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        MoveToBottomRight();
        AnimationService.StartIdle(ClippyTranslate, ClippyRotate, ClippyScale);
        blinkService.Start();
        sleepService.Start();
        appWatcherTimer.Start();
        cursorTrackingService.Start();
        if (settings.IsConfigured && archetypeService.CanSpeakAutomatically())
            await GenerateSpeechAsync(SpeechEvent.Greeting, automatic: true);
        else if (!settings.IsConfigured)
        {
            SetStatus(settingsStore.LoadError ?? "Настройте LLM через меню по правой кнопке.");
            OpenSettings();
        }
    }

    private void SetStatus(string text)
    {
        StatusText.Text = text;
        StatusPanel.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        MoveToBottomRight();
    }

    private void MoveToBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;
    }

    private void Say(string text)
    {
        if (stateService.IsSleeping || !IsVisible) return;
        stateService.SetSpeaking();
        SpeechText.Text = text;
        SpeechBubble.Visibility = Visibility.Visible;
        AnimationService.React(ClippyTranslate);
        AnimationService.AnimateSpeechBubble(SpeechBubble);
        speechTimer.Stop();
        speechTimer.Start();
    }

    private async Task<bool> GenerateSpeechAsync(SpeechEvent speechEvent, bool automatic = false,
        ActiveWindowInfo? window = null)
    {
        if (reallyExit || !IsVisible || settingsWindow != null || stateService.IsSleeping) return false;
        if (automatic && !archetypeService.CanSpeakAutomatically()) return false;
        if (!settings.IsConfigured)
        {
            if (!automatic) SetStatus("Добавьте API-ключ в настройках LLM.");
            return false;
        }
        if (speechRequest != null)
        {
            if (automatic || !speechRequestAutomatic) return false;
            // User actions take priority over background reactions, including waking up.
            CancelSpeech();
            lastRequestAt = DateTime.MinValue;
        }
        if (DateTime.UtcNow - lastRequestAt < TimeSpan.FromSeconds(2)) return false;
        var request = new CancellationTokenSource();
        speechRequest = request;
        speechRequestAutomatic = automatic;
        lastRequestAt = DateTime.UtcNow;
        SetStatus("Генерация…");
        var snapshot = SettingsStore.Copy(settings);
        var prompt = PromptLibrary.Build(snapshot, speechEvent, window?.ProcessName, window?.Title, recentLines);
        try
        {
            string text = await llm.GenerateAsync(snapshot, prompt.System, prompt.User, request.Token);
            if (request.IsCancellationRequested || speechRequest != request) return false;
            Say(text);
            recentLines.Enqueue(text);
            while (recentLines.Count > 5) recentLines.Dequeue();
            SetStatus("");
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (LlmException ex)
        {
            if (speechRequest == request) SetStatus(ex.Message);
            return false;
        }
        finally
        {
            if (speechRequest == request) speechRequest = null;
            request.Dispose();
        }
    }

    private void CancelSpeech()
    {
        speechRequest?.Cancel();
        speechRequest = null;
        SetStatus("");
    }

    private void OnClippyFellAsleep()
    {
        if (settingsWindow != null) return;
        CancelSpeech();
        speechTimer.Stop();
        stateService.SetSleeping();
        blinkService.Stop();
        spriteService.SetSleeping(true);
        SpeechBubble.Visibility = Visibility.Collapsed;
    }

    private async void OnUserActivityDetected()
    {
        if (!stateService.IsSleeping) return;
        if (!archetypeService.CanWakeFor(ActiveWindowService.GetActiveWindow())) return;
        WakeCharacter();
        await GenerateSpeechAsync(SpeechEvent.WakeUp, automatic: true);
    }

    private void WakeCharacter()
    {
        stateService.SetIdle();
        sleepService.ResetIdleCycle();
        spriteService.SetSleeping(false);
        blinkService.Start();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        CancelSpeech();
        blinkService.Stop();
        appWatcherTimer.Stop();
        sleepService.Stop();
        speechTimer.Stop();
        cursorTrackingService.Stop();
        trayService.Dispose();
        httpClient.Dispose();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (reallyExit) return;
        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        CancelSpeech();
        Hide();
    }

    private void ExitFromTray()
    {
        reallyExit = true;
        settingsWindow?.Close();
        CancelSpeech();
        Application.Current.Shutdown();
    }

    private void Clippy_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = System.Windows.Forms.Cursor.Position;
        pressPoint = new Point(position.X, position.Y);
        dragging = false;
        CharacterView.CaptureMouse();
        e.Handled = true;
    }

    // A movement threshold preserves dragging while a normal left click speaks.
    private void Clippy_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (pressPoint is not { } start || dragging || e.LeftButton != MouseButtonState.Pressed) return;
        var position = System.Windows.Forms.Cursor.Position;
        if (Math.Abs(position.X - start.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        dragging = true;
        CharacterView.ReleaseMouseCapture();
        try { DragMove(); }
        catch (InvalidOperationException) { }
        pressPoint = null;
    }

    private async void Clippy_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        bool clicked = pressPoint != null && !dragging;
        pressPoint = null;
        CharacterView.ReleaseMouseCapture();
        e.Handled = true;
        if (!clicked) return;
        if (stateService.IsSleeping) WakeCharacter();
        await GenerateSpeechAsync(SpeechEvent.Poked);
    }

    private async void AppWatcherTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsVisible || settingsWindow != null) return;
        ActiveWindowInfo window = ActiveWindowService.GetActiveWindow();
        if (ActiveWindowService.IsClipper(window) || string.IsNullOrWhiteSpace(window.ProcessName)) return;
        lastExternalWindow = window;
        if (!stateService.CanReact || !archetypeService.CanSpeakAutomatically()) return;
        string signature = $"{window.ProcessId}|{window.Title}";
        if (signature == lastWindowSignature) return;
        lastWindowSignature = signature;
        if (!settings.ShareWindowContext || !settings.AutomaticReactions || !settings.IsConfigured) return;
        var now = DateTime.UtcNow;
        if (now - lastAutomaticAt < TimeSpan.FromSeconds(45)) return;
        if (!cooldownService.CanReact(window.ProcessName, archetypeService.ApplyCooldown(TimeSpan.FromMinutes(2)))) return;
        // Limit attempts too, so a provider outage cannot create a request storm.
        lastAutomaticAt = now;
        cooldownService.MarkReacted(window.ProcessName);
        await GenerateSpeechAsync(SpeechEvent.AppChanged, automatic: true, window: window);
    }

    private async void OpenSettings()
    {
        if (reallyExit) return;
        if (settingsWindow != null) { settingsWindow.Activate(); return; }
        CancelSpeech();
        if (stateService.IsSleeping) WakeCharacter();
        sleepService.Stop();
        settingsWindow = new SettingsWindow(settings, settingsStore, llm);
        bool saved = settingsWindow.ShowDialog() == true;
        var updated = settingsWindow.SavedSettings;
        settingsWindow = null;
        if (reallyExit) return;
        sleepService.Start();
        if (!saved || updated == null) return;
        settings = updated;
        ApplyArchetype(settings.ArchetypeId);
        if (settings.IsConfigured) await GenerateSpeechAsync(SpeechEvent.Greeting);
        else SetStatus("Добавьте API-ключ в настройках LLM.");
    }

    private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();
        var phraseItem = new MenuItem { Header = "Сказать что-нибудь" };
        phraseItem.Click += async (_, _) => await GenerateSpeechAsync(SpeechEvent.SaySomething);
        var currentWindowItem = new MenuItem { Header = "Что я сейчас вижу?" };
        currentWindowItem.Click += async (_, _) =>
        {
            if (!settings.ShareWindowContext)
            {
                SetStatus("Передача контекста выключена. Включите её в настройках LLM.");
                return;
            }
            await GenerateSpeechAsync(SpeechEvent.CurrentWindow, window: lastExternalWindow);
        };
        var settingsItem = new MenuItem { Header = "Настройки LLM и промпты…" };
        settingsItem.Click += (_, _) => OpenSettings();
        var characters = new MenuItem { Header = "Персонаж" };
        foreach (var persona in CharacterPromptLibrary.All)
        {
            var item = new MenuItem
            {
                Header = persona.DisplayName, IsCheckable = true, IsChecked = settings.ArchetypeId == persona.Id
            };
            item.Click += async (_, _) => await ChangeArchetypeAsync(persona.Id);
            characters.Items.Add(item);
        }
        var resetItem = new MenuItem { Header = "Вернуться в угол" };
        resetItem.Click += (_, _) => MoveToBottomRight();
        var hideItem = new MenuItem { Header = "Спрятаться в трей" };
        hideItem.Click += (_, _) => HideToTray();
        var exitItem = new MenuItem { Header = "Выйти" };
        exitItem.Click += (_, _) => ExitFromTray();
        menu.Items.Add(phraseItem);
        menu.Items.Add(currentWindowItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(characters);
        menu.Items.Add(new Separator());
        menu.Items.Add(resetItem);
        menu.Items.Add(hideItem);
        menu.Items.Add(exitItem);
        menu.IsOpen = true;
    }

    private void ApplyArchetype(string id)
    {
        CancelSpeech();
        speechTimer.Stop();
        blinkService.Stop();
        archetypeService.Change(id);
        spriteService.SetSprites(archetypeService.Current.Sprites);
        stateService.SetIdle();
        sleepService.ResetIdleCycle();
        SpeechBubble.Visibility = Visibility.Collapsed;
        recentLines.Clear();
        cooldownService.ResetAll();
        lastAutomaticAt = lastRequestAt = DateTime.MinValue;
        lastWindowSignature = "";
        blinkService.Start();
    }

    private async Task ChangeArchetypeAsync(string id)
    {
        if (settingsWindow != null || settings.ArchetypeId == id) return;
        // Save a copy first so a persistence error leaves the active character untouched.
        var next = SettingsStore.Copy(settings);
        next.ArchetypeId = id;
        try { settingsStore.Save(next); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            SetStatus("Не удалось сохранить выбранного персонажа.");
            return;
        }
        settings = next;
        ApplyArchetype(id);
        await GenerateSpeechAsync(SpeechEvent.Greeting);
    }
}
