# Development guide

WordsCat is a C# / .NET 10 (LTS) Windows Forms application for Windows 11. The source project is `src/WordsCat/WordsCat.csproj`, targeting `net10.0-windows`; its output assembly is `WordsCat`.

## Build and verification

Install the .NET 10 SDK (x64). Framework-dependent output also requires .NET 10 Windows Desktop Runtime (x64), included with the .NET SDK for Windows. There is no repository `global.json`; select an installed .NET 10 SDK before building. `build.ps1`, `verify.ps1`, and the scripts under `scripts/` contain no separate SDK/runtime version pin.

From the repository root:

```powershell
.\build.ps1
.\verify.ps1 -IncludeUi
.\scripts\check-source.ps1
```

`build.ps1` publishes an x64, framework-dependent portable app to `publish/`. The project copies repository-owned assets, the OCR script, the default template, and both user guides into the runtime layout. Changing source directories must not change runtime data paths.

For end-user distribution, run `scripts/package-release.ps1`. It publishes a fresh self-contained Windows x64 build with the .NET 10 runtime patch selected by the SDK and produces a ZIP plus SHA-256 checksum under `dist/`. The old .NET 8 runtime pin has been removed; inspect `release-info.json` for the bundled versions. The release package requires no separate .NET install or build step. See [RELEASING.md](RELEASING.md) for archive validation and publishing.

The public packaging entry point is now `scripts/publish-release.ps1`, which delegates to the same packager. `-RequireClean` rejects uncommitted source for tag releases. The archive must include Apache-2.0 LICENSE/NOTICE, SECURITY.md, KaTeX/WebView2 licenses and original notices from the resolved .NET runtime packs. See CONTRIBUTING.md for human review and AI-assisted contribution requirements; do not submit generated changes without running verify and reviewing the complete diff.

`.github/workflows/ci.yml` runs Windows/.NET 10 build, non-UI verification and secret/source checks for main pushes and pull requests. `release.yml` handles v* tags with exact project-version matching, clean packaging, extracted offline verification and GitHub Release ZIP/checksum attachments. Local static checks cannot verify hosted runner or token permissions; observe the first hosted run after an authorized submission. Interactive checks stay in `-IncludeUi`.

The only NuGet dependency remains `Microsoft.Web.WebView2` 1.0.3595.46. It passes compilation and actual WebView2/KaTeX font and formula checks on .NET 10. No package upgrade was needed. Business behavior and compatibility identifiers are unchanged.

Validation (2026-10-31): SDK 10.0.403 and Windows Desktop Runtime 10.0.14; `build.ps1` succeeds without compiler warnings, `verify.ps1 -IncludeUi` passes, and `scripts/check-source.ps1` passes. New WinForms analyzer errors were resolved with explicit property serialization metadata; the nullable button-caption warning was resolved with an empty-string fallback. Run UI verification in a normal interactive desktop: the restricted launch environment did not finish WebView2 initialization. See [VALIDATION-v0.3.4.md](VALIDATION-v0.3.4.md).

`verify.ps1` creates a unique `.test-results/` directory for every run. The optional UI stage uses a real STA message loop, the installed WebView2 runtime, and synthetic inputs. It checks formula fonts, theme/language previews, popup transitions, sidebar behavior, and floating-menu behavior. No automated test reads real credentials or calls a model service.

Version 0.3.6 validation (2026-11-01): offline build without warnings, `verify.ps1 -IncludeUi`, source/secret guards, and the actual extracted ZIP's offline and desktop regressions pass. Both languages' guide/settings layouts were inspected in offline previews. See [VALIDATION-v0.3.6.md](VALIDATION-v0.3.6.md) for reports, archive checksum and test boundaries.

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

API keys live in Windows Credential Manager. New translations send only the current selection and instructions; follow-ups keep the current selection's text context in memory. The API transport accepts HTTPS and disallows redirects. `ITextModelClient` and `IImageModelClient` separate text and image input capabilities; `OpenAiCompatibleClient` implements both through chat completions. Business callers depend on the interfaces. The UI obtains saved credentials through the unchanged ReadDock target; the transport never loads credentials from files or environment variables.

Existing `base_url`, `model` and `vision_model` fields remain authoritative; default values are unchanged. `request_timeout_seconds` defaults to 100 and `max_retries` to 0 when absent, preserving old behavior. Explicit retries (0–5) cover 429/5xx and transport failures, using bounded backoff within one total deadline; 401 and malformed JSON are never retried. The default DeepSeek preset retains `thinking: disabled` and `image_url.detail: original`; other endpoints use standard image detail `auto` without the DeepSeek extension. Tests use only the fixed fake credential and local handlers.

Version 0.3.6 adds preset/URL/model/key controls and a modeless first-use guide. Missing `model_provider` is inferred from the existing URL; missing `service_guide_seen` defaults to false. The saved-key input is always blank and masked; blank preserves the key and cancel discards the draft. Presence checks inspect credential metadata only. The target remains `ReadDock.DeepSeek.ApiKey.v1`; new credential comments bind the HTTPS origin, while legacy comments remain accepted. Changing origins requires a replacement key; there is no file/environment fallback or per-provider key catalogue.

`ConnectionTester` uses a single fixed text message, eight output tokens, zero retries and a maximum 15-second timeout. Typed outcomes expose no raw headers, response bodies or exceptions. Draft keys/addresses must be saved before testing. `ICredentialVault` and injected local handlers isolate all automated checks from production credentials and services. Non-UI tests cover compatibility and status/error categories; `-IncludeUi` adds masked-field/save/cancel, immediate bilingual preset refresh with draft preservation, and nonmodal guide/settings-link checks. `--offline-preview` disables credential access and real connection probes. Refresh native ComboBox items after localization, guarding selection-change handlers to preserve unsaved drafts.

`TranslationPopup` uses compact `MathReadingView` without extra tabs, retaining the original action-row styling. Source/translation toggle buffers are local to the current popup; loading, errors and close clear them. Rendered document height adapts up to the existing cap; longer content scrolls. The main reader's same local-origin/CSP/textContent policy applies. The old approximate Unicode math conversion is removed.

Visual recognition sends the selected PNG and requests structured original/translation/uncertainty fields. Local Windows OCR runs through `scripts/ocr.ps1` (published as `ocr.ps1`). Developer diagnostics do not load production credentials or send live requests; the former `--vision-check` entry point is disabled.

Vocabulary entries are reviewed before saving. Template interpolation escapes plain text and rejects unknown placeholders. Saving preserves UTF-8 bytes, locks the target, appends deterministic Markdown, keeps bounded backups, and checks for external edits before undoing.

## Continuing work

Read `AGENTS.md`, `docs/PROJECT_STATE.md`, and `docs/REQUIREMENTS.md` before changes. Update the state, changelog, and validation evidence after completing work. Build output, local test output, secrets, and private samples must remain ignored. Keep commits local unless the user has requested a GitHub upload or publication.
