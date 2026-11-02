# WordsCat 0.3.4 / .NET 10 validation

Date: 2026-10-31. Windows 11 x64; .NET SDK 10.0.403; Windows Desktop Runtime 10.0.14. Application version remains 0.3.4.

## Build and verification

- `build.ps1`: passes without compiler warnings; output is `publish/WordsCat.exe`. The runtime configuration targets `net10.0` with .NET Core and Windows Desktop 10.0 framework requirements.
- Five new WFO1000 analyzer errors were fixed using existing property defaults or explicit runtime-only serialization metadata. One CS8601 warning was fixed with an empty-string fallback for a nullable button caption. No business logic, persisted data format or ReadDock compatibility identifier was changed.
- `verify.ps1 -IncludeUi`: passes in the normal interactive desktop. Reports: `.test-results/4195722ba080404aab7d4f74ebde9c5a/test-results.txt` and `ui/ui-test-results.txt` in that directory.
- `scripts/check-source.ps1`: passes for 124 source files, including the complete workspace/index secret scan, with no detected credential patterns.
- The first restricted-environment UI run did not finish WebView2 initialization. Its failure is retained at `.test-results/015152e39e314dc5a094750261a08698/ui/`; the successful normal-desktop rerun above is the acceptance evidence.

## Coverage

Offline regression covers secret redaction and HTTP error boundaries, settings and protected history, existing templates, Markdown writing and undo, automatic Chinese/English direction, mock API contracts, and real Windows OCR on a generated image.

Actual-window regression covers WebView2/KaTeX fractions and loaded Main/Math fonts, inert HTML, source/translation layout, language/theme preview and rollback, popup height, history retention prompts, minimize/close transitions, repeated floating-menu toggles and dismissal, and sidebar collapse/expand state and boundaries. WebView2 1.0.3595.46 is retained and passes these checks.

## Portable output

`build.ps1` creates framework-dependent output in `publish/`. `scripts/package-release.ps1` creates a fresh self-contained `dist/WordsCat-0.3.4-win-x64.zip` and `dist/SHA256SUMS.txt`. Extract the complete ZIP and run `WordsCat/WordsCat.exe`. A local archive is not automatically uploaded to GitHub.

- Rebuilt with `scripts/package-release.ps1 -Force`; 351 files, 53,268,461 bytes. Both included frameworks are version 10.0.14. SHA-256: `72627468ce21c08ccf21bc803f5b1b18723d4c6b5c1f5b7af3985d63c81f5d1a`.
- Fresh staging: `.build/release-b2f178f4a99b44538ff3db9a04b40dc7/WordsCat/`. Dependency, archive-content and private-data exclusions pass. `release-info.json` explicitly records the uncommitted source state; this is a local package, not a published GitHub Release.
- Extracted acceptance root: `.test-results/net10-portable-29762061ffe94a1180d04ff98ad8a456/`. `self-test/test-results.txt` and `ui-test/ui-test-results.txt` both pass on the actual ZIP contents. Empty `DOTNET_ROOT` / `DOTNET_ROOT_X64` and `host-trace.txt` confirm loading the extracted `coreclr.dll`, without resolving an installed shared runtime.
- SHA-256 matches `dist/SHA256SUMS.txt`; the extracted folder passes `scripts/check-secrets.ps1 -Workspace <extracted-app>` with exit code 0 and no findings.
- Computer Use inspected the extracted app with `--offline-preview` and a new `preview-visible/` directory: main-window brand, action icons, bilingual formulas, follow-up row and settings layout are visible; the API-key field is empty. No live connection button was used. The task's test preview process was closed afterward.
- The first restricted restore failed with a NuGet TLS error; the normal-permission retry fetched the official runtime packages and built successfully without warnings.

## Boundaries

Tests use isolated directories, synthetic text/images and local HTTP handlers. No real API credential is read and no model service is contacted. User Data and notebooks are untouched. This is not a live translation/recognition-quality assessment or a mixed-DPI/multi-monitor compatibility claim. No commit, push or GitHub publication is performed.
