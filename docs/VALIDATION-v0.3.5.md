# WordsCat 0.3.5 validation

Date: 2026-10-31. Native Windows Forms / .NET 10. No commit, push or remote release publication.

## Scope

The popup uses the existing main reader in compact mode, with source/translation toggling instead of the copy action. Both buffers remain local to the current selection. The main window and settings retain their existing appearance; no provider-selection controls are added.

The model transport now implements separate text/image interfaces. Existing DeepSeek defaults, provider-specific request parameters and Credential Manager target are preserved. Timeout/retry fields default to the old 100-second / zero-retry policy. Local Windows OCR, prompts, independent translation/follow-up context and deterministic notebook writes remain intact.

## Regression evidence

The isolated serial trial `.test-results/model-popup-trial-d99b835c228f48bea3c7aa0264391086/` passes both self-tests and real STA-window checks. It includes:

- Old settings without new fields, custom endpoint/model/OCR/template/notebook preservation, new option serialization and the unchanged credential-target constant (without invoking Credential Manager).
- Standard OpenAI-compatible text/image requests; existing DeepSeek wire-contract tests, including JSON output and original image detail.
- Success/token usage, 401 with no retries, 429 with default no-retry and explicit bounded retry, safe transport retry, non-JSON error handling, total timeout including backoff, and caller cancellation.
- Real WebView2 popup source/translation fractions and math fonts, inert HTML, short/long content sizing, vertical scrolling, and source-toggle reset after loading/errors; existing main-window/menu/sidebar regressions remain enabled.

All model requests in tests use the single fixed fake credential and local handlers. No real API key, user capture or model service is accessed. User Data and the exact legacy fixture bytes are preserved. Live recognition quality, mixed-DPI/multi-monitor behavior and provider-specific live compatibility are not assessed by these offline tests.

## Final acceptance and user font review

- `build.ps1 -Offline` succeeds without warnings. All dependencies were already cached; runtime restore for packaging used only `.build/packages` and the local NuGet cache.
- `verify.ps1 -IncludeUi` passes after the font adjustment. Final report root: `.test-results/c327184dcf1d4d44bfdd809a7c6b9430/`, including `test-results.txt` and `ui/ui-test-results.txt`. The real popup DOM asserts a 16px body font; source and translation formulas, inert HTML, scrolling, cap and reset checks pass. The earlier full run `.test-results/8a6887b3dcf14a7d89eea018992c35d2/` predates the font adjustment.
- `scripts/check-secrets.ps1`, invoked by final verification, passes for the entire workspace and staged blobs, including ignored binaries and ZIPs; no findings. A separate scan of the first extracted package also passed. The final workspace scan covers the updated 16px ZIP.
- Computer Use inspected the actual packaged popup through an isolated temporary harness loading only the popup and synthetic content. Clicking Show source displays English and rendered formulas; Show translation restores Chinese. Following user feedback, the popup was reduced from 18px to 16px; the main reader retains its existing size. The updated original and translation views were both inspected, and the preview was closed with Escape. No production main window, credential read or live request was involved.
- The final archive is `dist/WordsCat-0.3.5-win-x64.zip`, 53,276,844 bytes, 351 files, with .NET Core and Windows Desktop 10.0.14. SHA-256: `325df9642a243f29e613a3cc37c9fd27538e17a9b6c3a43b9753673a97cfee69`. Fresh staging: `.build/release-46aa2c3fbe4b4b60bd18fb14e180fa72/WordsCat/`. `dist/SHA256SUMS.txt` matches.
- The actual final ZIP was extracted under the unique temporary popup-test directory; its executable passes offline self-tests in `final-package-self-test/`. The same final extracted components were used for the 16px Computer Use inspection. No Data, captures, settings or test outputs are included in the archive. The local working-tree package is marked `sourceDirty: true`; it is not a GitHub Release.
- Exact legacy fixture bytes, the unchanged local OCR script, clean diff formatting and unchanged Git index were confirmed. No commit, push, history rewrite, real-key access or model-service request was performed.
