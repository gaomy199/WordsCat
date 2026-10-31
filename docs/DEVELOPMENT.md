# Development guide

WordsCat is a C# / .NET 8 Windows Forms application for Windows 11. The source project is `src/WordsCat/WordsCat.csproj`; its output assembly is `WordsCat`.

## Build and verification

From the repository root:

```powershell
.\build.ps1
.\verify.ps1 -IncludeUi
.\scripts\check-source.ps1
```

`build.ps1` publishes an x64, framework-dependent portable app to `publish/`. The project copies repository-owned assets, the OCR script, the default template, and both user guides into the runtime layout. Changing source directories must not change runtime data paths.

For end-user distribution, run `scripts/package-release.ps1`. It publishes a fresh self-contained Windows x64 build with the pinned .NET runtime and produces a ZIP plus SHA-256 checksum under `dist/`. The release package requires no separate .NET install or build step. See [RELEASING.md](RELEASING.md) for archive validation and publishing.

`verify.ps1` creates a unique `.test-results/` directory for every run. The optional UI stage uses a real STA message loop, the installed WebView2 runtime, and synthetic inputs. It checks formula fonts, theme/language previews, popup transitions, sidebar behavior, and floating-menu behavior. No automated test reads real credentials or calls a model service.

## Components

| Directory | Responsibility |
| --- | --- |
| `UI` | Main window, editable cards, settings, lightweight result popup, controls and theme |
| `Reading` | Local formula pages, resource mapping, transient WebView2 cache lifecycle |
| `Persistence` | DPAPI-encrypted translation history and retention |
| `Services` | API requests, prompts, source-language detection, deterministic Markdown saving, local OCR integration |
| `Platform` | Hotkeys, selection copying, capture overlay, floating entry and Windows Credential Manager |
| `Localization` | Chinese/English captions for application-owned text |
| `Models` | Configuration, notebook routing, template and entry models, compatibility migrations |
| `Diagnostics` | Isolated self-tests and actual-window regression checks |

The existing `ReadDock` namespace, credential target, local virtual host, mutex names, and notebook markers are compatibility identifiers. Do not rename them as part of cosmetic project organization.

## Runtime resources

The runtime expects `assets/math/`, `ocr.ps1`, and `vocabulary.template.json` beside the executable. `assets/math/reader.html` and its scripts/styles/fonts share a local WebView2 origin. Inject dynamic text through `textContent`; never interpret model or selected text as HTML. Keep KaTeX's license and verify actual font loading after resource changes.

The legacy fixture `fixtures/vocabulary-v0.1.json` is checked by SHA-256. Its bytes must not be normalized; `.gitattributes` disables text conversion for it.

## API credential security checks

Real credentials must never be placed in source, scripts, docs, tests, fixtures, logs, exception details, exported configuration, or translation history. Production uses the existing Windows Credential Manager target; there is no environment/file fallback. Maintenance, automated tests and developer diagnostics never load real keys. Tests use the single explicit fake value in `SecuritySelfTests` and local HTTP handlers. Do not modify user `Data/` directories.

Before committing:

```powershell
.\scripts\check-secrets.ps1
.\verify.ps1
.\scripts\check-source.ps1
```

Enable the local pre-commit hook once:

```powershell
git config core.hooksPath .githooks
```

The hook runs the guard before a commit. The guard scans the entire workspace (including hidden local configuration, ignored build/publish/test output, compiled strings and ZIP/NuGet contents) and the actual staged Git blobs. It excludes Git internals/history, which must be handled separately. It never accesses Credential Manager or secret environment values. Matches fail with a nonzero exit code and print only `file:line`; `-IncludeTypes` adds the classification without printing values. Read errors also fail closed. `[REDACTED]` and the exact fixed test credential are the only accepted credential-value exceptions.

Run `scripts/test-secret-guard.ps1` for synthetic UTF-8, UTF-16, compiled-string, ZIP-entry and staged-only negative cases. It creates a uniquely named disposable directory under `.test-results/`, never commits, proves a clean working copy cannot hide a staged finding, and removes its test files and isolated index afterward. Do not introduce broad folder exclusions or line-wide suppression comments to make the guard pass.

The shared `SecretRedactor` handles diagnostics, model echoes, status/error displays, settings string serialization and newly recorded history. Request Authorization headers are used only in memory for the request and are not logged; HTTP error bodies are never included in errors. The legacy developer `--vision-check` command is disabled and cannot read saved credentials or make live requests.

For an offline maintenance build with already cached dependencies, use `build.ps1 -Offline`; this passes `--no-restore` and never downloads packages. Missing cached dependencies must be reported rather than fetched when networking is prohibited.

## Runtime data and API behavior

Settings and mutable data default to `Data/` beside the executable. An explicit `--data-dir` overrides that location. `--offline-preview` skips credential reading and uses synthetic content. Keep user data and private fixtures out of commits and distribution archives.

API keys live in Windows Credential Manager. New translations send only the current selection and instructions; follow-ups keep the current selection's text context in memory. The API transport accepts HTTPS, disallows redirects, has a total timeout, and does not retry automatically.

Visual recognition sends the selected PNG and requests structured original/translation/uncertainty fields. Local Windows OCR runs through `scripts/ocr.ps1` (published as `ocr.ps1`). Developer diagnostics do not load production credentials or send live requests; the former `--vision-check` entry point is disabled.

Vocabulary entries are reviewed before saving. Template interpolation escapes plain text and rejects unknown placeholders. Saving preserves UTF-8 bytes, locks the target, appends deterministic Markdown, keeps bounded backups, and checks for external edits before undoing.

## Continuing work

Read `AGENTS.md`, `docs/PROJECT_STATE.md`, and `docs/REQUIREMENTS.md` before changes. Update the state, changelog, and validation evidence after completing work. Build output, local test output, secrets, and private samples must remain ignored. Keep commits local unless the user has requested a GitHub upload or publication.
