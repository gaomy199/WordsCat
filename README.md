# WordsCat

WordsCat (单词猫) is a Windows desktop reading companion for on-demand Chinese–English translation and vocabulary collection in Obsidian-compatible Markdown.

Current version: **0.3.4**. Built with C# and .NET 8 Windows Forms.

## Download and run

Download **[WordsCat-0.3.4-win-x64.zip](https://github.com/gaomy199/WordsCat/releases/download/v0.3.4/WordsCat-0.3.4-win-x64.zip)** from the [latest release](https://github.com/gaomy199/WordsCat/releases/latest). Extract the complete archive, open the `WordsCat` folder, and double-click `WordsCat.exe`.

The Windows x64 portable ZIP includes .NET 8. You do not need to download this repository, install the SDK, or build the app. Keep the extracted files together. Configure your API key and notebooks in Settings. `SHA256SUMS.txt` is provided with the release for integrity checks. GitHub's automatically generated **Source code** archives are for developers; choose the named Windows ZIP to run the app.

Current binary support: **Windows 11 x64**. Formula reading uses Microsoft Edge WebView2 Runtime; text views remain available if it is missing. When upgrading, exit the old app and preserve its `Data/` folder and Markdown notebooks.

## Features

- Translate selected text or a screen capture; detect Chinese or English automatically.
- Read LaTeX formulas offline with bundled KaTeX assets, edit recognized text, and inspect the original capture.
- Show capture results in a lightweight popup before opening the main window.
- Ask follow-up questions about the current selection without mixing independent translations.
- Review and edit vocabulary cards before saving to multiple Markdown notebooks.
- Keep recent translation history locally, encrypted for the current Windows account.
- Switch between Chinese and English UI, customize appearance, and collapse the history sidebar.
- Use a draggable floating entry, tray menu, and global keyboard shortcuts.

## Requirements

- Windows 11 (x64).
- The Release ZIP includes .NET 8 Windows Desktop Runtime. Source builds require .NET 8 SDK or later; the default development build uses an installed .NET 8 Windows Desktop Runtime.
- Microsoft Edge WebView2 Runtime for formula reading. Text views remain available if initialization fails.
- A DeepSeek API key for model-based translation and visual recognition. Windows OCR is also available for local text recognition.

## Build and run

These instructions are for developers. For normal use, download the portable ZIP above.

Run from the repository root in PowerShell:

```powershell
.\build.ps1
.\publish\WordsCat.exe
```

Keep the complete `publish/` directory together, including `assets/math/`, `ocr.ps1`, and the bundled vocabulary template. Configure the API key and notebook destinations in Settings. When upgrading, exit the old app and preserve its `Data/` directory and existing Markdown notebooks.

To build a self-contained portable release from a fresh staging directory:

```powershell
.\scripts\package-release.ps1
```

The ZIP and `SHA256SUMS.txt` are written to `dist/`. See [release instructions](docs/RELEASING.md).

## Verify locally

```powershell
.\verify.ps1
.\verify.ps1 -IncludeUi
.\scripts\check-source.ps1
```

Verification uses isolated data, synthetic text/images, local mock HTTP responses, and the installed Windows OCR/WebView2 runtime. It does not read a real API key or contact a model service. UI verification requires an interactive Windows desktop.

For an offline preview:

```powershell
.\publish\WordsCat.exe --offline-preview --data-dir "$PWD\.test-results\preview"
```

## Repository layout

```text
src/WordsCat/
  WordsCat.csproj       Application project
  Program.cs           Startup and diagnostic entry points
  UI/                  Windows, dialogs, controls, and theme
  Reading/             Formula reader and WebView2 lifecycle
  Persistence/         Encrypted local translation history
  Services/            Translation, vocabulary storage, and OCR service
  Platform/            Windows capture, clipboard, tray, and credentials
  Localization/        Application-owned UI captions
  Models/              Configuration, templates, and data models
  Diagnostics/         Offline and real-window regression checks
assets/                Default icon and bundled formula resources
scripts/               Windows OCR helper
templates/             Default vocabulary template
fixtures/              Public regression fixtures; private/ is ignored
docs/                  User guide, development notes, and validation records
build.ps1              Build the portable application
verify.ps1             Run isolated local verification
```

Internal `ReadDock` namespaces and compatibility identifiers are retained so existing settings, Windows credentials, templates, and notebook markers keep working.

## Data and credentials

The API key is stored in Windows Credential Manager, not in Git or settings JSON. Local configuration, translation history, private captures, build output, test output, and notebook data are excluded from source control. Visual recognition sends the selected image to the configured model service; local OCR recognizes the image on the computer, then model translation sends the recognized text.

New translations are independent. Follow-ups only use the current selection. Vocabulary saving is deterministic code after an editable preview; the model does not rewrite an entire notebook.

## Documentation

- [User guide](docs/USER_GUIDE.md)
- [中文使用说明](docs/USER_GUIDE.zh-CN.md)
- [Development guide](docs/DEVELOPMENT.md)
- [Changelog](CHANGELOG.md)
- [Latest validation](docs/VALIDATION-v0.3.4.md)
- [Project state](docs/PROJECT_STATE.md) and [confirmed requirements](docs/REQUIREMENTS.md)
- [Third-party notices](THIRD_PARTY_NOTICES.md)

Some project continuity and historical validation documents are maintained in Chinese. Repository paths and the main entry documentation use English.
