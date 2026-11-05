# 0.3.18 validation · 2026-11-05

This revision fixes vocabulary field sizing and main-window typography/spacing. User data and credentials remain outside the maintenance workflow. All generated inputs and configuration live in isolated `.test-results/` directories. No document sample or image was uploaded, no real key was read, and no model service was contacted.

## Build and automated desktop checks

- `build.ps1 -Offline` compiles version 0.3.18 into `publish/` without compiler warnings.
- Direct real-STA desktop regression: `.test-results/layout-desktop-0.3.18-d8c5ca87bf87467bb7d5df4e1ad4e743/ui-test-results.txt` passed.
- Final `verify.ps1 -IncludeUi` passed on the normal interactive desktop, including the working-tree/index secret guard, non-UI self-tests, generated-image Windows OCR and the actual UI suite. Report: `.test-results/593aa97ecf3b444fa629299c8eaac540/test-results.txt`; UI report: `.test-results/593aa97ecf3b444fa629299c8eaac540/ui/ui-test-results.txt`.
- `git diff --check` passed. `fixtures/vocabulary-v0.1.json` has the same Git blob hash as HEAD; no fixture byte change.
- The new regressions toggle one, two and all supplement groups, including scrolling, and check the final word-forms editor height, content-table height, retained manual edits, removed editors and Markdown. Both main reading panes report 16px Chinese glyphs, 18px English/body text and 20.7px KaTeX. Additional bounds checks cover the follow-up input's vertical center and clearance above footer actions.
- The existing desktop suite also covers real KaTeX rendering/font loading, inert HTML, popup switching/heights, history, settings preview/rollback, sidebar, floating menu, modal collection and local mock enrichment/save. Model mocks use only `SecuritySelfTests.FakeCredential`; credentials are injected or disabled.

Initial runs exposed stale regression expectations from earlier untested versions: truncated output now uses `ModelOutputLimitException`, the custom provider follows the added Gitee option, and a popup can briefly return JSON null before its document loads. Those test expectations/waits were synchronized. The new layout test explicitly normalizes its synthetic notebook configuration. The complete desktop rerun passes after these corrections.

The restricted-desktop launch could not initialize WebView2 (`.test-results/layout-0.3.18-c94181ba98ac4003b398be6105a44ca6/`). The successful run above executes on the normal interactive desktop; the failure is not treated as passing evidence.

## Computer Use inspection

Used the computer-use skill with `@oai/sky` to inspect the real `publish/WordsCat.exe`, launched with `--offline-preview --data-dir .test-results/ui-review-v0.3.18`. The preview uses a synthetic notebook name, orange accent, no floating entry and disabled production credentials.

Observed:

- Main window in normal and maximized layouts: compact titles/subtitles/reading tabs, visible source and translation areas, a clear gap above footer actions, and aligned follow-up frame/button. No overlap was observed among the checked controls and icons.
- The current-notebook dropdown and the settings service-preset dropdown: centered closed text; the preset's native expanded list retains readable rows and selection.
- Source/translation swap: Chinese appears at the smaller size on the left as well as on the right, with formulas retaining their scale.
- Vocabulary editor: click IPA on/off once, then turn all three supplements on and off and scroll to the final word-forms field. The remaining field has its normal height with no accumulating blank area. The preview has no key, so enabling supplement generation produces the local missing-key message without an HTTP request; successful model-response behavior is covered separately by the fixed-fake in-memory regression.
- Cancelled the word editor, exited the preview with Ctrl+Q, and confirmed no preview window remained.

## Scope and remaining limits

Only `publish/` is updated; no ZIP, commit or upload is produced. The historical `fixtures/vocabulary-v0.1.json`, third-party assets/notices, credential targets, notebooks and custom-template files are preserved. Current-desktop checks do not establish every mixed-DPI/multi-monitor layout or real OCR/model recognition quality.
