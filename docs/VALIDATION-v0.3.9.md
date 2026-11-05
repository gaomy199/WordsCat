# WordsCat 0.3.9 validation

Date: 2026-11-02. Local build/package only, as requested; no commit, push, tag or remote publication.

## Behavior

- Enabled IPA is immediately after lemma and before part of speech in the real editor and standard Markdown. Standard standalone IPA lines move in memory; inline custom placeholders retain their layout.
- Meaning combines the current contextual sense first with other common senses. Old contextual-meaning labels update to Meaning in memory. A stale model's other_meanings response merges into meaning instead of creating a separate editor.
- Common word forms replaces the previous other-senses optional field and switch. Each line contains a related form, POS and Chinese meaning; prompts prefer common word-family derivatives/inflections and correct spelling, and do not request unrelated synonyms. Model results are locally limited to three nonempty distinct lines; manually entering a fourth item is rejected before file creation/write. Existing source-sentence validation, editable preview and deterministic saving remain.
- Existing supplement_senses values migrate to include_word_forms; the new key takes precedence if both occur. Only the new key is serialized. Template files, existing notebooks, credentials and portable paths remain compatible. Old reserved optional fields upgrade in memory without rewriting user files.

## Build and tests

- `build.ps1 -Offline`: success without compiler warnings; framework-dependent `publish/` is 0.3.9.
- Initial isolated offline test: `.test-results/trial-0.3.9-01e992b871f94767a4a9ee54678fd443/`, passed.
- `verify.ps1 -PublishPath .test-results/portable-0.3.9-0a50f5f39b74468c8db24e133cc3a5b7/WordsCat -IncludeUi`: passed on the normal interactive desktop. Final report `.test-results/d7daa816aaa545cb8a7c5aeab81568e0/`, with `test-results.txt`, `ui/ui-test-results.txt` and `ui/vocabulary-ui-results.txt`.
- Coverage includes IPA field/render order and idempotence, unified meanings and old model response compatibility, legacy option migration and precedence, AI three-item cap, manual overflow rejection without writing, old template label conversion in memory, original-template byte preservation, portable relocation, real switch save/cancel/name, and all optional fields through popup local mocks and actual Markdown save. Prior UI/KaTeX/security/OCR regressions pass as well.
- Final `scripts/check-source.ps1` passed after package/UI generation: 152 source files checked; full workspace/index secret guard (including the new ZIP, ignored binaries and test output) passed with no findings. `git diff --check` passed. Old fixture SHA-256 is unchanged: `2958D12F12799DEEFA646FE3F7ADF1D3AC0098B60FDACD2D3530437D26A64121`.

## Computer Use

Inspected the actual extracted package's editor components through an ignored temporary local WinForms host under `.test-results/visual-0.3.9-helper/`. It used synthetic generalizable data, the fixed fake credential and an in-memory client; no HTTP calls. The host temporarily gave test dialogs taskbar entries to let Computer Use locate them; product taskbar behavior was unchanged. Editor inspection confirmed lemma → IPA → POS and the unified Meaning label. The raw Markdown preview showed the same order and three correctly spelled forms (generalize, generalization, generalized) with POS and meanings, followed by AI example/translation and collocations. There was no separate other-senses section. The helper was closed. Switch renaming and persistence are covered by the actual-window automated regression.

## Archive

- File: `dist/WordsCat-0.3.9-win-x64.zip`, 53,328,081 bytes, 357 files under `WordsCat/`.
- SHA-256: `025dd994c2130eddd1011ceec8df8bb583f57a43adf930af39446210cb268373` (also `dist/SHA256SUMS.txt`).
- Self-contained win-x64; Microsoft.NETCore.App and Microsoft.WindowsDesktop.App 10.0.14. No separate .NET install is needed. Uses the installed WebView2 Runtime, with text fallback when absent.
- Built by `scripts/publish-release.ps1 -Offline` using fresh staging `.build/release-7e90e6fb9750434b94049ff51895425a/WordsCat/`. Archive file set and mandatory legal/runtime files are checked by the packager, which rejects Data/settings/history/caches/private fixtures. It records sourceRevision 19c2e22e14084f1d472d47ed3d824ed8b3651c8c and sourceDirty true, consistent with the user's no-commit request.
- Extracted offline and actual-window tests pass. Existing older ZIPs remain; the checksum file identifies the new ZIP.

## Boundaries

No production credentials were read, user Data modified, user sample uploaded or model service contacted. Vocabulary quality and spelling are directed by the prompt and reviewed by the user; synthetic-output tests establish routing/layout/limits, not live model accuracy. Existing mathematical recognition, mixed-DPI and external reader compatibility limits remain. The archive is unsigned and is a local uncommitted build, not a GitHub Release.
