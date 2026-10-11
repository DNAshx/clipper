# Clipper

Небольшой десктопный помощник в духе старого Microsoft Clippy для Windows 10/11.

Проект написан на C# + WPF и задуман как живой персонаж поверх рабочего стола: он реагирует на активные приложения, периодически моргает, засыпает при бездействии, просыпается при вводе, умеет показывать случайные реплики и сворачиваться в системный трей.

## Возможности

- Always-on-top окно без рамки
- Перетаскивание мышкой
- Comic Sans MS в репликах
- Случайные фразы
- Реакции на активные приложения
- Отдельные реакции для:
  - Visual Studio
  - Unity
  - Tor Browser
  - Telegram
  - Notepad++
  - FL Studio
  - Git / Git Bash
  - qBittorrent
  - PowerShell
  - Windows Terminal
  - cmd
  - VPN-клиентов
  - WireGuard
  - AmneziaVPN
  - Happ
  - и других приложений
- Cooldown между репликами
- Idle-анимация
- Моргание
- Сон после бездействия
- Пробуждение при движении мыши или нажатии клавиш
- Контекстное меню по ПКМ
- Пункт "Что я сейчас вижу?"
- Возврат в правый нижний угол
- Сворачивание в системный трей
- Иконка приложения и трея
- Четыре персонажа с независимыми глазами и слежением за курсором

## Пример поведения

Visual Studio:

> Похоже, ты пытаешься создать баг.

Unity:

> Ты сохранил сцену?

Tor Browser:

> Опять за закладкой собрался, пёс?

VPN:

> Обычный человек нажимает «подключиться». Ты строишь транспортную сеть.

qBittorrent:

> Архивы научных трудов качаем?

## Персонажи и глаза

Персонаж выбирается через **ПКМ → Персонаж**:

- **Скрепыш** — обычное поведение.
- **Металкорщик** — просыпается при активности в FL Studio.
- **Готичка** — говорит по запросу пользователя, просыпается молча.
- **Гопник** — реагирует без дополнительного cooldown.

База персонажа остаётся одной и той же при моргании и сне. Белки, зрачки и веки отрисовываются отдельно; зрачки следят за курсором и скрываются при закрытых глазах. Положение и контуры глаз описаны в JSON. Для картинок со встроенными глазами используется маска при отрисовке, исходные PNG сохраняются.

Неиспользуемые исходники и варианты лежат в подпапках **references**.

## Структура проекта

~~~text
clipper/
├─ Archetypes/
│  ├─ default.json
│  ├─ metalcore.json
│  ├─ goth.json
│  └─ gopnique.json
├─ Assets/
│  ├─ original/       # обычный Скрепыш и иконка
│  ├─ metalcore/      # база, глаза, зрачки и веки
│  ├─ goth/           # Готичка и исходные варианты
│  └─ gopnique/       # Гопник и исходные варианты
├─ Data/
│  └─ ReactionLibrary.cs
├─ Models/
│  ├─ AppReaction.cs
│  ├─ ArchetypeDefinition.cs
│  └─ EyeLayers.cs
├─ Services/
│  ├─ ActiveWindowService.cs
│  ├─ AnimationService.cs
│  ├─ AppReactionService.cs
│  ├─ ArchetypeService.cs
│  ├─ BlinkService.cs
│  ├─ CharacterSpriteService.cs
│  ├─ ClippyStateService.cs
│  ├─ CursorTrackingService.cs
│  ├─ ImageService.cs
│  ├─ ReactionCooldownService.cs
│  ├─ SleepService.cs
│  └─ TrayService.cs
├─ App.xaml
├─ App.xaml.cs
├─ MainWindow.xaml
├─ MainWindow.xaml.cs
└─ clipper.csproj
~~~

## Сборка и запуск

Нужны Windows и .NET 10 SDK. Из корня репозитория:

~~~powershell
dotnet build clipper.slnx
dotnet run --project clipper/clipper.csproj
~~~

Папки **Assets** и **Archetypes** автоматически копируются при сборке и публикации.

## Бета-релиз

Готовые сборки: [GitHub Releases](https://github.com/toxic8yte/clipper/releases).
Первая бета: **v0.1.0-beta.1**. Архивы x64 и ARM64 включают .NET.
Распакуйте весь ZIP и запустите `clipper.exe`, сохранив папки Assets и Archetypes рядом.
Инструкция по совместимости и самостоятельной сборке: [clipper/RELEASE.md](clipper/RELEASE.md).