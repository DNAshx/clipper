# Clipper 0.1.0-beta.1

Первая бета-версия: четыре персонажа, реакции на приложения, анимация глаз,
слежение за курсором, сон при бездействии и системный трей.
Возможны ошибки: это предварительный релиз.

## Скачать и запустить

- `clipper-0.1.0-beta.1-win-x64.zip` — обычные ПК с Intel/AMD, 64-битная Windows.
- `clipper-0.1.0-beta.1-win-arm64.zip` — Windows на ARM64.

Распакуйте **весь архив** в отдельную папку и запустите `clipper.exe`.
Папки `Assets` и `Archetypes` должны оставаться рядом с EXE.
.NET включён в архив; устанавливать SDK или runtime для запуска не требуется.
Смена персонажа и выход доступны через ПКМ; приложение также имеет значок в трее.

Для Windows 10 и 11 используются одни и те же архивы соответствующей архитектуры.
Рекомендуется актуальная Windows 11. Запуск x64 из распакованного ZIP проверен
на Windows 10 22H2; это не означает официальную поддержку ОС Microsoft.
ARM64-сборка скомпилирована, но запуск на ARM-устройстве ещё не проверен.
Windows 7/8/8.1 этим релизом не поддерживаются.
Поддерживаемые Microsoft версии ОС для .NET 10:
https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md

## Собрать из исходников

Нужны Windows, PowerShell и [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
Из корня репозитория:

```powershell
dotnet build clipper/clipper.csproj -c Release
dotnet run --project clipper/clipper.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File clipper/publish-beta.ps1
```

Скрипт создаёт самостоятельные ZIP для x64 и ARM64 в `clipper/artifacts`.
Для одной архитектуры передайте `-Runtime win-x64`, `win-arm64` или `win-x86`.
32-битную сборку можно собрать этим же скриптом при необходимости.
Исходные варианты картинок из `references` и локальные аудиофайлы в релиз не входят.

Версия задаётся в `clipper/clipper.csproj`. Следующие беты: `0.1.0-beta.2`,
`0.1.0-beta.3`; стабильный релиз: `0.1.0`. Тег GitHub имеет префикс `v`.
