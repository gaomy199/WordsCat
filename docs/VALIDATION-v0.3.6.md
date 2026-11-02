# WordsCat 0.3.6 validation

Date: 2026-11-01. Native Windows Forms / .NET 10. No commit, push or remote publication.

## Scope and changes

- `UI/SettingsDialog.cs`: DeepSeek/custom OpenAI-compatible presets, HTTPS Base URL/model/key controls, always-empty masked key entry, preserve/replace/cancel semantics and classified localized connection testing. New key/address drafts must be saved before testing saved credentials.
- `UI/SoftControls.cs`: rebuild native ComboBox item strings after localization. Guard preset/theme/language handlers during refresh, preserving selected values and service/key drafts without resetting defaults or recursing.
- `UI/ServiceSetupGuide.cs`, `UI/MainWindow.cs`: dismissible modeless first-use guide, settings link and persisted seen state; text views remain usable. Offline preview disables real credential access and probes.
- `Platform/CredentialVault.cs`, `Platform/WindowsIntegration.cs`: injected fake/disabled vaults for tests/previews; production existence checks use credential metadata without decoding the key. Preserve `ReadDock.DeepSeek.ApiKey.v1`; new metadata binds the HTTPS origin, legacy comments remain accepted. No file/environment credential fallback.
- `Services/ConnectionTest.cs`, `Services/OpenAiCompatibleClient.cs`: one fixed message, eight output tokens, zero retries and at most 15 seconds; safe typed results for missing key, 401, 402/429, network/timeout and invalid response shape. Raw payloads, headers and exceptions are not displayed. Translation defaults remain unchanged.
- `Models/Models.cs`, `Localization/UiText.cs`: backward-compatible provider/guide fields, old-URL preset inference and Chinese/English labels/results.
- `Diagnostics/ServiceSetupSelfTests.cs`, existing diagnostic runners: fixed-fake/local-handler compatibility and status tests; desktop-only form/guide checks run under `-IncludeUi`.
- README, bilingual guides, development/release/requirements notes, changelog and project state synchronized with completed behavior and the local archive.

No translation feature, OCR-path change, user-data migration or new key source was added.

## Automated evidence

- `build.ps1 -Offline`: passes without compiler warnings on SDK 10.0.403, targeting .NET 10. Publishing uses cached dependencies; no network service was contacted.
- `verify.ps1 -IncludeUi`: passes. Report root: `.test-results/1f81c32a7de24a39b150fb4d8d3b9b86/`, containing `test-results.txt` and `ui/ui-test-results.txt`. Its `scripts/check-secrets.ps1` stage passes over working files and staged blobs, including ignored outputs/archives.
- Non-UI coverage: legacy settings/defaults and credential metadata compatibility; missing-key short-circuit without reading a key or constructing a client; success, 401, 402, 429, transport failure, timeout, HTML and invalid JSON shape; minimal payload and zero retries; bilingual result mapping.
- Actual-window coverage: empty masked key/no saved-key read on opening; explicit local probe; unsaved drafts not sent; custom/default presets; cancel/no write, blank/preserve and replacement/save; no key in JSON; offline safety; modeless guide, reader availability, dismissal persistence, configured-key skip, settings link and English guide/401 text. All previous formula, popup, sidebar, history and floating-menu checks remain enabled.
- After strengthening the bidirectional language-switch assertion to check URL/model/key draft preservation, the final ZIP was rebuilt locally. Its actual extracted executable passes self-tests and the complete actual-window suite: `.test-results/portable-0.3.6-253dd5cc8d644835bac225d8ee9cba52/self-test/test-results.txt` and `ui/ui-test-results.txt`.
- `scripts/check-source.ps1`: passes for 134 source files. Its full `check-secrets.ps1` invocation runs after packaging and extracted tests, covering the final ZIP/binaries and staged blobs; no findings.
- Legacy fixture bytes match HEAD exactly, and the Git index remains unchanged. Diff whitespace checks pass.

The previous failing trial `.test-results/service-setup-trial-0d2dd9ac901b482e8a9807de5156f8eb/` remains as historical evidence. Its preset-text refresh failure is resolved by the final checks above.

## Visual inspection

Computer Use inspected the actual extracted 0.3.6 application with `--offline-preview` and newly created temporary data directories. Checked Chinese guide/DeepSeek settings, English guide/custom-service settings, protected empty key input, compact preset frame, localized notes, readable buttons and offline connection-result text. Preview windows were closed. These previews used synthetic configuration without a real key; masked typing/save and immediate language transitions were additionally checked by the real STA regression suite.

## Portable package

- Archive: `dist/WordsCat-0.3.6-win-x64.zip`.
- Size: 53,290,006 bytes; 351 files under the `WordsCat/` archive root.
- SHA-256: `225b926d261685e16b321a9cc65f003d26980494f48c8005176ffc35d824314c`, matching `dist/SHA256SUMS.txt`.
- Fresh staging: `.build/release-1dd9e54815f8439492e19e8480cda3bb/WordsCat/`.
- Bundled frameworks: Microsoft.NETCore.App and Microsoft.WindowsDesktop.App 10.0.14. Self-contained Windows x64; no separate .NET install required.
- Extracted to `.test-results/portable-0.3.6-253dd5cc8d644835bac225d8ee9cba52/`. Empty `DOTNET_ROOT` self-test succeeds; `runtime-trace.txt` confirms `coreclr.dll` resolves inside the extracted app directory.
- Packaging verifies required OCR/template/math-font/license resources and rejects private directories/files; no Data, captures, settings, histories, caches or debugging symbols in the ZIP. The validated 0.3.5 ZIP is retained.
- Source revision `d260c6d3906cc9b587a81046f0c8250c94f254b1`, `sourceDirty: true`. This is a local working-tree package, not a GitHub Release.

## Boundaries and remaining work

No required work remains. No production key was read, model service contacted, user Data changed, Git index/history modified or source/binary uploaded. Tests use the single fixed fake credential and local handlers. Live-provider compatibility, recognition quality, billing, mixed-DPI/multi-monitor coverage and code signing remain outside this offline task. A public release would require a separately authorized source submission and clean-revision rebuild.
