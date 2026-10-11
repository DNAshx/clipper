using clipper.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Separator = System.Windows.Controls.Separator;


namespace clipper
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer appWatcherTimer;
        private readonly DispatcherTimer speechTimer;
        private readonly AppReactionService reactionService;
        private readonly ArchetypeService archetypeService;
        private readonly ClippyStateService stateService;
        private readonly TrayService trayService;

        private ActiveWindowInfo? lastExternalWindow;

        private readonly BlinkService blinkService;
        private readonly CharacterSpriteService spriteService;
        private readonly CursorTrackingService cursorTrackingService;
        private readonly SleepService sleepService;

        private string lastWindowSignature = "";
        private bool reallyExit = false;

        private readonly ReactionCooldownService cooldownService;

        public MainWindow()
        {
            InitializeComponent();

            trayService = new TrayService();
            archetypeService = new ArchetypeService("default");

            trayService.ShowRequested += ShowFromTray;
            trayService.ExitRequested += ExitFromTray;

            sleepService = new SleepService();

            sleepService.IdleReached += OnClippyFellAsleep;
            sleepService.ActivityDetected += OnUserActivityDetected;

            stateService = new ClippyStateService();

            reactionService = new AppReactionService();

            cooldownService = new ReactionCooldownService();


            spriteService = new CharacterSpriteService(CharacterCanvas, EyesCanvas, ClippyImage);
            spriteService.SetSprites(archetypeService.Current.Sprites);
            blinkService = new BlinkService(spriteService.SetBlinking);
            cursorTrackingService = new CursorTrackingService(CharacterCanvas, spriteService);

            appWatcherTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };

            appWatcherTimer.Tick += AppWatcherTimer_Tick;

            speechTimer = new DispatcherTimer
            {
                Interval = AnimationService.SpeechAnimationDuration
            };
            speechTimer.Tick += (_, _) =>
            {
                speechTimer.Stop();
                if (stateService.CurrentState == ClippyState.Speaking)
                    stateService.SetIdle();
            };
        }

        private void Window_Loaded(object sender,  RoutedEventArgs e)
        {
            MoveToBottomRight();

            AnimationService.StartIdle(
                ClippyTranslate,
                ClippyRotate,
                ClippyScale);

            blinkService.Start();
            sleepService.Start();
            cursorTrackingService.Start();
            appWatcherTimer.Start();

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
            if (stateService.IsSleeping)
                return;

            stateService.SetSpeaking();

            SpeechText.Text = text;

            AnimationService.React(ClippyTranslate);
            AnimationService.AnimateSpeechBubble(SpeechBubble);

            speechTimer.Stop();
            speechTimer.Start();
        }

        private void OnUserActivityDetected()
        {
            if (!stateService.IsSleeping)
                return;

            ActiveWindowInfo window = ActiveWindowService.GetActiveWindow();

            if (!archetypeService.CanWakeFor(window))
                return;

            WakeUp();
        }
        private void ChangeArchetype(string archetypeId)
        {
            archetypeService.Change(archetypeId);

            var archetype = archetypeService.Current;

            blinkService.Stop();
            spriteService.SetSprites(archetype.Sprites);
            blinkService.Start();

            stateService.SetIdle();
            sleepService.ResetIdleCycle();
            lastWindowSignature = "";
            cooldownService.ResetAll();

            Say($"Теперь я {archetype.DisplayName}.");
        }

        private void OnClippyFellAsleep()
        {
            speechTimer.Stop();
            stateService.SetSleeping();

            blinkService.Stop();

            spriteService.SetSleeping(true);

            SpeechText.Text = "Хрррр...";
        }
        private void WakeUp()
        {
            sleepService.ResetIdleCycle();
            stateService.SetWakingUp();

            spriteService.SetSleeping(false);

            blinkService.Start();

            stateService.SetIdle();

            if (archetypeService.CanSpeakAutomatically())
                Say("А? Я не спал.");
            else
                SpeechText.Text = "";
        }
        private void Window_Closed(object? sender,  EventArgs e)
        {
            blinkService.Stop();
            appWatcherTimer.Stop();
            speechTimer.Stop();
            sleepService.Stop();
            cursorTrackingService.Stop();

        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (reallyExit)
                return;

            e.Cancel = true;

            Hide();
        }

        private void ExitFromTray()
        {
            reallyExit = true;

            blinkService.Stop();
            sleepService.Stop();
            cursorTrackingService.Stop();
            appWatcherTimer.Stop();
            speechTimer.Stop();

            trayService.Dispose();

            Application.Current.Shutdown();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            if (e.ClickCount == 2)
            {
                Say(reactionService.GetGenericPhrase());
                return;
            }

            DragMove();
        }


        private void AppWatcherTimer_Tick(object? sender, EventArgs e)
        {
            ActiveWindowInfo window =
                ActiveWindowService.GetActiveWindow();

            // Clipper всегда продолжает наблюдать за внешним окном,
            // даже если архетип молчит или спит.
            if (!ActiveWindowService.IsClipper(window))
            {
                lastExternalWindow = window;
            }

            // Пробуждение разрешает SleepService только после активности пользователя.
            if (stateService.IsSleeping)
                return;

            // Например гот сам разговор не начинает.
            if (!archetypeService.CanSpeakAutomatically())
                return;

            if (!stateService.CanReact)
                return;

            string signature =
                $"{window.ProcessId}|{window.Title}";

            if (signature == lastWindowSignature)
                return;

            lastWindowSignature = signature;

            string cooldownKey = window.ProcessName;

            TimeSpan cooldown = window.ProcessName.ToLowerInvariant() switch
            {
                "firefox" => TimeSpan.FromMinutes(3),
                "devenv" => TimeSpan.FromSeconds(45),
                "unity" => TimeSpan.FromSeconds(45),
                "telegram" => TimeSpan.FromMinutes(1),
                "qbittorrent" => TimeSpan.FromMinutes(2),
                _ => TimeSpan.FromSeconds(30)
            };

            cooldown =
                archetypeService.ApplyCooldown(cooldown);

            if (!cooldownService.CanReact(
                    cooldownKey,
                    cooldown))
            {
                return;
            }

            string? phrase =
                reactionService.TryGetReaction(window);

            if (!string.IsNullOrWhiteSpace(phrase))
            {
                Say(phrase);

                cooldownService.MarkReacted(
                    cooldownKey);
            }
        }

        private void Window_MouseRightButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            ContextMenu menu = new ContextMenu();

            MenuItem phraseItem = new MenuItem
            {
                Header = "Сказать что-нибудь"
            };

            phraseItem.Click += (_, _) =>
            {
                Say(reactionService.GetGenericPhrase());
            };

            MenuItem currentWindowItem = new MenuItem
            {
                Header = "Что я сейчас вижу?"
            };

            currentWindowItem.Click += (_, _) =>
            {
                if (lastExternalWindow == null)
                {
                    Say("Я пока ничего не успел подсмотреть.");
                    return;
                }

                Say(
                    $"{lastExternalWindow.ProcessName}\r\n" +
                    $"{lastExternalWindow.Title}"
                );
            };

            MenuItem resetItem = new MenuItem
            {
                Header = "Вернуться в угол"
            };

            resetItem.Click += (_, _) =>
            {
                MoveToBottomRight();
            };

            MenuItem archetypeMenu = new MenuItem
            {
                Header = "Персонаж"
            };

            MenuItem defaultItem = new MenuItem
            {
                Header = "Скрепыш"
            };

            defaultItem.Click += (_, _) =>
            {
                ChangeArchetype("default");
            };

            MenuItem gothItem = new MenuItem
            {
                Header = "Готичка"
            };

            gothItem.Click += (_, _) =>
            {
                ChangeArchetype("goth");
            };

            MenuItem gopnikItem = new MenuItem
            {
                Header = "Гопник"
            };

            gopnikItem.Click += (_, _) =>
            {
                ChangeArchetype("gopnique");
            };

            MenuItem metalcoreItem = new MenuItem
            {
                Header = "Металкорщик"
            };

            metalcoreItem.Click += (_, _) =>
            {
                ChangeArchetype("metalcore");
            };

            archetypeMenu.Items.Add(defaultItem);
            archetypeMenu.Items.Add(gothItem);
            archetypeMenu.Items.Add(gopnikItem);
            archetypeMenu.Items.Add(metalcoreItem);

            MenuItem exitItem = new MenuItem
            {
                Header = "Убить Скрепыша"
            };

            exitItem.Click += (_, _) =>
            {
                ExitFromTray();
            };

            MenuItem hideItem = new MenuItem
            {
                Header = "Спрятаться в трей"
            };

            hideItem.Click += (_, _) =>
            {
                Hide();
            };

            menu.Items.Add(phraseItem);
            menu.Items.Add(currentWindowItem);
            menu.Items.Add(resetItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(archetypeMenu);

            menu.Items.Add(hideItem);

            menu.Items.Add(exitItem);

            menu.IsOpen = true;
        }
    }
}