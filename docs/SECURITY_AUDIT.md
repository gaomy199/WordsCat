# API credential security audit

Date: 2026-10-31. Scope: API credential safety only. All work is local and uncommitted.

## Findings

No suspected hardcoded credential was found by the initial text scan or the subsequent working-tree/index guard. The initial inventory inspected 710 text files. The guard additionally inspects hidden/local configuration, ignored build/publish/test output, UTF-8 and UTF-16 compiled strings, and ZIP/NuGet entries. It does not scan Git history or retrieve credentials from Windows Credential Manager or environment variables. User `Data/` was not modified, and existing encrypted user history was not decrypted during this task.

The finding list is empty. If future findings occur, reports may contain only `file:line:type`, never matching values or excerpts. Binary findings use line 1, archive paths use `archive!entry`, and staged findings use the `staged/` prefix. Read errors fail closed with line 0.

An empty native Authorization template in a third-party DLL initially produced a false positive by crossing the string terminator. The detector now stops at control characters; no vendor/output directory was excluded to suppress the finding.

## Changes made in this task

- `.gitattributes`, `.gitignore`, `AGENTS.md`, `build.ps1`, `verify.ps1`, `CHANGELOG.md`.
- `.githooks/pre-commit`.
- `scripts/check-secrets.ps1`, `scripts/test-secret-guard.ps1`, `scripts/check-source.ps1`.
- `src/WordsCat/Security/SecretRedactor.cs`, `src/WordsCat/Security/SecretStringConverter.cs`.
- `src/WordsCat/Diagnostics/SecuritySelfTests.cs`, `src/WordsCat/Diagnostics/SelfTests.cs`, `src/WordsCat/Diagnostics/UiSelfTests.cs`.
- `src/WordsCat/Services/Services.cs`, `src/WordsCat/Models/Models.cs`, `src/WordsCat/Persistence/ReadingHistory.cs`, `src/WordsCat/Platform/WindowsIntegration.cs`, `src/WordsCat/Program.cs`.
- `src/WordsCat/UI/Ui.cs`, `src/WordsCat/UI/MainWindow.cs`, `src/WordsCat/UI/SettingsDialog.cs`, `src/WordsCat/UI/TranslationPopup.cs`, `src/WordsCat/UI/Dialogs.cs`, `src/WordsCat/Localization/UiText.cs`.
- `docs/DEVELOPMENT.md`, `docs/PROJECT_STATE.md`, `docs/SECURITY_AUDIT.md`.

The earlier uncommitted portable-release work was preserved. Its version, release scripts, README/download text, guides and project-file changes were not committed, pushed or published by this security task. No business capability was added.

## Security boundaries

- Saved production API credentials retain the Windows Credential Manager target and ReadDock compatibility identifiers. No file/environment fallback is added. Settings connection checks use only the saved credential; a newly entered value must be saved first.
- Developer diagnostics no longer load production credentials or make live requests. The old `--vision-check` entry point is disabled. Tests use the explicit fixed fake value and local HTTP handlers only.
- The shared redactor covers credential patterns, Authorization values and caller-supplied in-memory credentials. Exception/status displays, diagnostic files, model echoes, settings string serialization and history persistence use this boundary.
- HTTP errors expose only safe status messages, not response bodies or Authorization headers. A sensitive transport exception is replaced without retaining its sensitive inner exception.
- Existing history is sanitized in memory when loaded and before subsequent normal persistence. The regression uses newly generated synthetic encrypted records; it does not open actual user history.
- The guard reads actual Git index blobs, so cleaning a working copy cannot hide a staged credential. Only the exact fake value and redaction marker are accepted value exceptions; there are no line-wide suppression comments or build/output-folder exclusions. Git internals/history remain outside the guard.

## Validation

- `scripts/test-secret-guard.ps1`: passed UTF-8, UTF-16, compiled-string, ZIP-entry and staged-only negative cases. Synthetic probes are constructed in memory, output contains locations only, and the disposable test files/index are removed in `finally`. No commit occurs.
- `build.ps1 -Offline`: passed without warnings, using `--no-restore` and cached dependencies only.
- `verify.ps1`: passed, including the full workspace/index guard, security sink regressions and the existing local regressions. Report: `.test-results/f1663b1600784b21998b1d8d52b4842c/`.
- The exact legacy template fixture Git blob is unchanged.
- `scripts/check-source.ps1`: passed for 124 source files, including another complete workspace/index/archive guard run with no findings.
- The disabled developer vision diagnostic was invoked with an unused image argument and isolated output; it returned the fixed disabled result without reading an image or credentials.
- Script parsing and `git diff --check` passed. The real index and HEAD remain unchanged.

## Remaining work outside this task

The user handles removal of revoked credentials from Git history. No commit, push, rebase or history rewriting was performed. Hook activation is documented (`git config core.hooksPath .githooks`); this task does not alter that local Git setting. Previous build/release copies remain local and ignored; they were scanned, but the previous release-publication task is not resumed.
