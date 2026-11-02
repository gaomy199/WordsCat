# WordsCat 0.3.8 validation

Date: 2026-11-02. Local source and framework-dependent publish build; no commit or distribution upload.

## Changes covered

- Four native slide switches persist vocabulary options: other common senses on by default; IPA, AI examples/translations and collocations off by default. Legacy JSON receives the same defaults. Options extend a template in memory; existing custom files, fields and layouts remain intact. Existing explicitly configured custom fields remain available when switches are off.
- Contextual meaning/POS and source sentence stay separate from supplementary senses and AI examples. Model output is parsed into enabled fields, edited before save and rendered/appended by deterministic code. Source examples remain checked against the current input; rejected/missing examples clear their corresponding translations.
- Internal paths serialize relative to settings.json's directory, while runtime paths are absolute. Relative resolution does not depend on the process working directory. Missing old absolute Data paths recover only when the corresponding current file exists. External vaults and missing external custom templates retain their explicit locations.
- Main-window version badge reads the application version instead of a hard-coded old version.

## Automated evidence

- `build.ps1 -Offline`: succeeds without compiler warnings; `publish/WordsCat.exe` is version 0.3.8.
- First `verify.ps1 -IncludeUi`: isolated non-UI checks passed, including the added vocabulary/path tests, secret guard and generated-image Windows OCR. Report: `.test-results/610b492b16f54a35ab3ace791dc81a0b/`. The restricted-desktop UI stage failed at WebView2 initialization (`{error:initializing}`); this is retained as failed evidence and is not a passing UI result.
- `scripts/check-source.ps1`: passed, 151 source files checked; its full workspace/index secret guard also passed with no findings. The old fixture retains SHA-256 `2958D12F12799DEEFA646FE3F7ADF1D3AC0098B60FDACD2D3530437D26A64121`. `git diff --check` passed.
- The first normal-desktop verify attempt stopped in its guard because the crashed preview's WebView2 process retained locks on isolated temporary cache files. Only that preview's browser process was stopped and its `.test-results/.../Cache` folder removed; no scan exclusions were added. `verify.ps1 -IncludeUi` then passed in the normal desktop environment. Final report: `.test-results/e5d50f6ecfe54fe88adc36e620acf217/`; both `test-results.txt` and `ui/ui-test-results.txt` pass, with added switch evidence in `ui/vocabulary-ui-results.txt`. The full popup/local-handler assertions passed, including all supplementary fields in editable controls and saved Markdown.

New regressions cover all-off/default/independent/all-on fields, an illustrative segment noun sense retained apart from the contextual verb sense, ignored disabled model fields, deterministic rendering, custom layout preservation, relative-path save/reload after moving an isolated directory, notebook/template byte preservation, missing legacy Data recovery, external paths, switch save/cancel persistence and real editor fields. The popup's local HTTP mock exercises every enabled field through automatic completion, editing and Markdown save, retaining popup ownership and restore behavior.

## Computer Use inspection

An isolated offline preview used `.test-results/visual-0.3.8-6b7fdb6d36254cff91a8f121e36eea6b/`, synthetic built-in reader text and all four options enabled for inspection. The Chinese settings page displayed four complete left/right switches and relative notebook/template paths. Clicking the IPA thumb moved it left and changed it to the off color. Scrolling exposed all four template buttons and the path explanation without overlap; Cancel restored the reader. The preview was exited. A restricted launch had produced a WebView2 startup error; the inspection used a normal-desktop launch.

## Boundaries

No production credentials were read, no model services contacted, no user sample uploaded and no user Data directory modified. Tests use the fixed fake credential, fake vault and local handlers. Supplementary vocabulary quality depends on the configured model; this task verifies prompts, field routing, preview and save behavior with synthetic responses, not live dictionary accuracy. Existing math recognition, mixed-DPI and reader compatibility limits remain. No new ZIP is created; older dist archives remain their original versions.
