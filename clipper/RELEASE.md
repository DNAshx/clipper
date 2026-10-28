# Clipper AI — portable Windows build

This is the LLM-enabled fork of [toxic8yte/clipper](https://github.com/toxic8yte/clipper), maintained at [DNAshx/clipper](https://github.com/DNAshx/clipper).

It includes the upstream four-character update: Skrepysh, Goth Girl, Gopnique, and Metalcore, with animated layered eyes, cursor tracking, and character-specific behavior. This fork adds OpenAI, Claude, and Gemini connections, live model catalogs, encrypted settings, generated dialogue, and editable personality prompts for each character.

## Run

Extract the entire archive to a folder and run `clipper.exe`. Keep **Assets** and **Archetypes** beside the executable. The .NET runtime is included.

On first launch, add a provider API key and select a model. API usage is billed by your provider. If you already use Clipper AI, your settings are loaded from `%LOCALAPPDATA%\ClipperAI\settings.dat`; existing keys and customized prompts are preserved.

Right click the character to open **Персонаж** (character selection) or **Настройки LLM и промпты…** (settings). In settings, **Персонаж** lets you edit each character's personality; **Промпты** contains shared instructions and event prompts. A left click generates a character-specific response. Dragging moves the character. Exit through the tray or right-click menu.

Goth Girl responds to explicit interactions and does not initiate automatic dialogue. Metalcore wakes automatically for FL Studio; clicking can wake any character. Context sharing and automatic application reactions remain optional and off by default.

Use x64 on a 64-bit Intel/AMD Windows PC and ARM64 on Windows on ARM. ARM64 execution has not been verified on an ARM device. Reference artwork is not included in portable builds.

## Build

From the repository root, with Windows and the .NET 10 SDK:

```powershell
dotnet build clipper.slnx -c Release
dotnet run --project tests/Clipper.Tests -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File clipper/publish-beta.ps1
```

The publish script creates x64 and ARM64 ZIPs under `clipper/artifacts`. Pass `-Runtime win-x64` to build x64 only. See the repository's main README for provider setup, workspace troubleshooting, privacy details, and complete controls.
