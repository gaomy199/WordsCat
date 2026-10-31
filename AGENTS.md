# WordsCat development conventions

## Start here

Read `docs/PROJECT_STATE.md` and `docs/REQUIREMENTS.md` before inspecting the relevant source and `git status`. These files preserve project state across chats.

This repository is the official source workspace for WordsCat (formerly ReadDock), a C# / .NET 8 Windows Forms application for Windows 11. The project is `src/WordsCat/WordsCat.csproj`. Keep the native stack; do not create a website, cloud environment, or plugin to replace it.

## Implementation

- Complete routine changes directly while preserving existing settings, notebooks, custom templates, and credentials.
- Show capture results in a lightweight popup near the selection. Open the full main window only when the user requests it.
- Prefer the configured DeepSeek visual model for mathematical literature; retain local Windows OCR.
- Keep new translations independent. Follow-ups belong only to the current selection.
- Save Markdown with deterministic code after editable preview and validation. Do not let a model rewrite an entire notebook.
- Keep current and default notebook choices separate. Check duplicates only in the selected target file.
- Keep default fields minimal, as specified in `docs/REQUIREMENTS.md`; do not add source, page, notes, or collection date without a user request.
- Let the user choose priorities. Possible future directions are not approved development tasks.
- Use English repository paths and the English component layout. Preserve `ReadDock` compatibility identifiers unless a separate migration is explicitly required.

## Build and verification

Run `build.ps1` to generate `publish/`, and `verify.ps1` for isolated automated verification. Include `-IncludeUi` for actual-window regression checks. Run `scripts/check-source.ps1` before uploading source. Tests must not read a real API key or contact a model service. UI changes also require inspection with available Computer Use tools; simulated model output does not prove recognition quality.

## API credential security

- Never put real credentials in code, scripts, documentation, fixtures, tests, logs, exception details, exported settings, or translation history, under any circumstances.
- Production API requests obtain saved credentials only from Windows Credential Manager. Preserve the `ReadDock.DeepSeek.ApiKey.v1` target and all other ReadDock compatibility identifiers. Do not add environment-variable or file-based credential fallbacks.
- Tests and developer diagnostics must never read real keys from Credential Manager, environment variables, or files. Use only the fixed fake credential defined by the security self-test and local mock handlers; diagnostics must not contact model services.
- Do not retrieve production credentials from Credential Manager or secret environment variables during maintenance or security scans. Workspace scanning may inspect file bytes but must never reveal matched values. Do not modify user `Data/` directories. Use isolated test directories and synthetic inputs.
- Before any commit, run both `verify.ps1` and `scripts/check-secrets.ps1`. Enable `.githooks/pre-commit` as described in `docs/DEVELOPMENT.md`. The guard checks working files and staged Git blobs, including ignored build, publish, test and archive outputs.
- Secret findings may be reported only as file, line number, and type. Never print matched content, header values, source excerpts containing suspected credentials, or a suspected credential in a commit message.

API keys are stored in Windows Credential Manager and must never be written to documentation, tests, logs, or Git. Do not commit `Data/`, runtime configuration, private captures, build output, or test output. `fixtures/private/` contains local-only samples and remains ignored. The user's earlier one-image network test authorization has already been used; obtain specific authorization for any future upload of a user sample.

## Finish each task

Update `docs/PROJECT_STATE.md` with the current version, changes, validation results, unresolved issues, and remaining work. Record version changes in `CHANGELOG.md`. Keep source, documentation, and test evidence consistent.

Keep commits local unless the user requests an upload or distribution in the current task. When publishing, exclude user data and credentials. Preserve third-party notices and exact bytes of `fixtures/vocabulary-v0.1.json`.
