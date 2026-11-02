# WordsCat

WordsCat (单词猫) is a Windows desktop reading companion for Chinese–English translation and vocabulary collection in Obsidian-compatible Markdown.

[Download](https://github.com/gaomy199/WordsCat/releases) · [中文使用说明](docs/USER_GUIDE.zh-CN.md) · [User guide](docs/USER_GUIDE.md) · [Changelog](CHANGELOG.md)

## Features

- Translate selected text or screen captures, with automatic Chinese/English direction detection.
- Read formulas with bundled KaTeX, edit recognized text, and compare it with the original capture.
- View results in a lightweight popup; switch between source and translation and select words to save.
- Ask follow-up questions about the current selection, keeping independent translations separate.
- Review vocabulary before saving to multiple Markdown notebooks, with duplicate checks and undo.
- Keep recent translations locally, encrypted for the current Windows account.
- Choose DeepSeek or a custom OpenAI-compatible model service, with a connection test and first-use setup guide.
- Use Chinese or English UI, theme options, a collapsible history sidebar, a floating entry, and hotkeys.

## User installation

**Supported system: Windows 11 x64.** Portable releases include the .NET 10 Windows Desktop Runtime.

1. Open [GitHub Releases](https://github.com/gaomy199/WordsCat/releases) and choose a release with a `WordsCat-VERSION-win-x64.zip` asset.
2. Download the Windows ZIP and its matching `SHA256SUMS.txt`. GitHub's **Source code** archives contain source that requires a developer build.
3. Verify the ZIP, extract the complete archive to a writable folder, then run `WordsCat/WordsCat.exe`.
4. Configure a model service and Markdown notebook under **Settings**.

Microsoft Edge WebView2 Runtime enables formula reading. Text views remain usable if WebView2 is unavailable. Keep all extracted files together, including `assets/math/`, `ocr.ps1`, and `vocabulary.template.json`.

### Verify a download

Run this in the download folder, using your archive's filename:

```powershell
Get-FileHash .\WordsCat-0.3.7-win-x64.zip -Algorithm SHA256
```

Compare the result with the matching line in `SHA256SUMS.txt`; case does not matter. Use the download only if the hashes match.

Builds are currently **unsigned**, so Windows SmartScreen may warn about an unfamiliar app. Check the release source and checksum before deciding whether to run it. A checksum checks integrity against the published value; code signing provides a separate identity check.

### First run

1. Open **Settings → Models & OCR** and choose **DeepSeek** or **Custom OpenAI-compatible**. For a custom service, enter its HTTPS Base URL and model name. Visual recognition requires an image-capable model.
2. Enter your API key and save settings. Reopen Settings and choose **Test connection** to check the saved text-service connection.
3. Under **Notebooks**, add a Markdown file, such as one in your Obsidian vault, and choose a default notebook.
4. Select text and translate it, or paste text into the source editor and choose **Translate source**.

The key field is masked and opens empty. Leaving it blank preserves the saved key. On first launch without a key, the setup guide can be dismissed while you use text views. Interface language is selected under **Appearance**.

### Shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+Alt+D` | Translate selected text |
| `Ctrl+Alt+S` | Capture and translate |
| `Ctrl+Alt+W` | Save vocabulary |
| `Ctrl+Alt+R` | Show or hide the main window |

Before upgrading, exit the old app and preserve its `Data/` folder and Markdown notebooks. See the [English guide](docs/USER_GUIDE.md) or [中文使用说明](docs/USER_GUIDE.zh-CN.md) for detailed usage.

## Data and credentials

API keys are stored only in Windows Credential Manager. Settings show an empty masked key field, and exported configuration contains no key. Existing ReadDock credentials remain compatible.

Visual recognition sends the selected image to the configured provider. Windows OCR recognizes the image locally; model translation then sends the recognized text. Review mathematical symbols against the original capture. Provider data handling follows the provider's policy.

Recent completed source/translation pairs are encrypted locally for the current Windows account. Vocabulary is written to the Markdown notebooks you choose. Runtime data, private captures, credentials and generated output are excluded from source control.

## Developer builds

Install the **.NET 10 SDK** on Windows. From the repository root:

```powershell
.\build.ps1
.\publish\WordsCat.exe
```

The project is [src/WordsCat/WordsCat.csproj](src/WordsCat/WordsCat.csproj), targeting `net10.0-windows`. The default development build uses the installed .NET 10 Windows Desktop Runtime. Preserve the English component layout and `ReadDock` compatibility identifiers.

### Verify changes

```powershell
.\verify.ps1
.\scripts\check-secrets.ps1
.\scripts\check-source.ps1
```

For UI changes, also run `./verify.ps1 -IncludeUi` in an interactive Windows desktop and inspect the actual windows. Automated verification uses isolated data, synthetic inputs, fake credentials and local mock handlers.

To preview the interface without reading credentials or calling a model service:

```powershell
.\publish\WordsCat.exe --offline-preview --data-dir "$PWD\.test-results\preview"
```

### Create a portable ZIP

```powershell
.\scripts\publish-release.ps1
```

| Output | Purpose |
| --- | --- |
| `publish/` | Development build using the installed .NET runtime |
| `dist/WordsCat-VERSION-win-x64.zip` | Self-contained package for users |
| `dist/SHA256SUMS.txt` | Checksum for the packaged ZIP |

The packager uses a fresh staging directory and includes .NET, math resources, OCR, the template and license notices. `dist/` is ignored by Git. `package-release.ps1` remains a supported entry point; `-Offline` uses restored dependencies, and `-RequireClean` requires a clean source checkout.

### CI and releases

CI builds on `windows-latest` with .NET 10 and runs verification without UI plus secret/source checks. A matching `vX.Y.Z` tag triggers clean-source packaging, extracted-package verification and creation of a GitHub Release with the Windows ZIP and checksum.

Check [Actions](https://github.com/gaomy199/WordsCat/actions) for hosted run results. Follow [RELEASING.md](docs/RELEASING.md) for version/tag requirements and publication steps. Local packaging creates files under `dist/`; publication is handled by the release workflow.

## Documentation and contributing

- [English user guide](docs/USER_GUIDE.md) and [中文使用说明](docs/USER_GUIDE.zh-CN.md)
- [Development guide](docs/DEVELOPMENT.md) and [release instructions](docs/RELEASING.md)
- [Contributing](CONTRIBUTING.md), including human diff review and verification for AI-assisted changes
- [Security reporting](SECURITY.md)
- [Changelog](CHANGELOG.md) and [0.3.7 validation record](docs/VALIDATION-v0.3.7.md)
- [Project state](docs/PROJECT_STATE.md) and [confirmed requirements](docs/REQUIREMENTS.md)

## License

WordsCat is licensed under [Apache-2.0](LICENSE). Copyright 2026 gaomy199; see [NOTICE](NOTICE). Bundled dependencies retain their own licenses, listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
