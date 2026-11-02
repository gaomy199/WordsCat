# WordsCat 0.3.7 validation

Completed: 2026-11-02. Automated verification and packaging were completed on 2026-11-01; visual inspection and handoff records were finished on resumption. Native C#/.NET 10 Windows Forms; uncommitted local working tree. No GitHub upload, tag, hosted workflow run or repository-settings change.

## Popup collection fix

The modal editor previously read only the main reader selection, while the topmost popup could cover the editor. The popup now exposes its rendered/raw selection; MainWindow snapshots that selection before hiding the popup, opens the existing editor with the current recognized source as context, and restores the same popup after save/cancel without showing the main window. The editor inherits the owner's topmost state. Translation, recognition, prompts, provider defaults, credential sources and deterministic saving remain unchanged.

Actual-window regression covers source/translation selections, a different stale main selection, existing automatic enrichment through a local HTTP handler and fake vault, modal ownership/topmost state with the popup hidden, restored popup/main visibility, cancellation without a write, saving the selected entry to an isolated notebook, and clearing stale selection for a new capture. No real credential or model service is used. A trial exposed test setup racing the queued preview Shown event; the test now waits for initialization before supplying its synthetic source. Final suites pass.

## Release and open-source files

- `scripts/publish-release.ps1` delegates to the existing fresh-directory win-x64/self-contained packager; default output is ZIP and SHA256SUMS.txt in already-ignored `dist/`. `-RequireClean` rejects dirty source before packaging, confirmed with a negative local check.
- Packaging requires math/OCR/template/font dependencies, Apache LICENSE/NOTICE, SECURITY.md, KaTeX/WebView2 notices and original resolved .NET runtime licenses/third-party notices. SHA-256 comparisons confirm runtime legal files retain exact package bytes.
- `ci.yml`: main pushes/pull requests, windows-latest, .NET 10, build, verify without UI, secret/source checks, read-only contents and no persisted checkout credentials.
- `release.yml`: v* tags, exact tag/project-version check, clean-source packaging, extracted offline verification and secret scan, then GitHub Release ZIP/checksum attachments. Contents-write is scoped to the release job; the temporary GitHub token is used only for upload. Existing release assets are not overwritten.
- README separates user installation and developer builds, explains unsigned SmartScreen behavior and SHA256 comparison. SECURITY.md documents private reporting and Credential Manager storage; CONTRIBUTING.md requires executed verify checks and human diff review for AI-assisted contributions. Issue/PR templates are included.
- Apache-2.0 selected by the user, copyright 2026 gaomy199. Third-party notices distinguish the bundled .NET runtime from installed WebView2, KaTeX resources, application-owned files and operating-system fonts.

## Executed checks

- `build.ps1 -Offline`: passes without compiler warnings.
- `verify.ps1 -IncludeUi`: passes, including the new popup collection checks and all existing regressions. Report: `.test-results/6eb0ea29b3ca4150a0c1f6da8f3b9104/` (`test-results.txt`, `ui/ui-test-results.txt`). Its initial `check-secrets.ps1` scans working/staged files and ignored binaries/archives with no findings.
- Actual final ZIP extracted and both offline/self-test and complete real-window suites pass: `.test-results/portable-0.3.7-169d6e66e7a242ce8f30b40c4fd9eb2f/` (`self-test/test-results.txt`, `ui/ui-test-results.txt`). Packaged bilingual guides match source hashes.
- Static workflow checks: reviewed YAML structure/indentation, triggers, .NET/runner versions, permissions, no UI or pull_request_target execution, safe environment use for ref names, matching tag/clean-source gates and expected assets. Native PowerShell parser accepts all scripts and all four inline release blocks. No GitHub workflow was run locally; runner behavior and upload authorization remain unverified until a hosted run.
- Final `scripts/check-source.ps1` passes for 147 source files, repeated successfully after handoff-document updates on 2026-11-02. Its `check-secrets.ps1` stage covers the final ZIP, binaries, ignored outputs and staged blobs, with no findings. Record: `.test-results/source-check-v0.3.7.txt`. The index and legacy fixture bytes remain unchanged.

## Completed visual inspection

On 2026-11-02, Computer Use inspected final extracted-package components through an isolated temporary harness with a fake vault and local HTTP handler. A real mouse double-click selected `robust` in the rendered popup. Clicking Save word opened the unobscured editor with the selected word/lemma, mock meaning, recognized source sentence and mock sentence translation populated. Cancel restored the original popup, and the preview was closed. No production credentials, live services or user data were accessed.

The harness made its test windows taskbar-visible solely so the inspection tool could target them; no product taskbar behavior was changed. Production ownership/topmost/hide/restore behavior, both views, save/cancel and automatic enrichment are also covered by the real STA tests above. Initial attempts with the temporary harness exposed handle-recreation/targeting limitations; these were resolved in the harness without modifying the packaged application. This is UI evidence with synthetic model output, not recognition-quality evidence.

## Local portable archive

- `dist/WordsCat-0.3.7-win-x64.zip`: 53,316,272 bytes; 357 files under `WordsCat/`.
- SHA-256: `ffa6e90d5281aa7284db21047c1afb378747d2a3c156fed51e67cc0c3fb068f0`, also in `dist/SHA256SUMS.txt`.
- Archive checksum reconfirmed on 2026-11-02; packaged binary and guides are unchanged.
- Bundled Microsoft.NETCore.App and Microsoft.WindowsDesktop.App 10.0.14.
- Fresh stage: `.build/release-1652f810794d42f78b6d49539300482f/WordsCat/`.
- Source revision `d260c6d3906cc9b587a81046f0c8250c94f254b1`, sourceDirty true. This is a local working-tree archive, not a published Release. Previous packages are retained.

## External follow-up and boundaries

The owner must enable GitHub private vulnerability reporting before publication so the SECURITY.md form is available. This task prepares the files without changing account/repository settings. After an authorized source submission/tag, observe the first hosted CI/Release run; static checks cannot replace it. Builds remain unsigned. Live recognition quality, model-provider compatibility and mixed-DPI/multi-monitor coverage are outside these local mock checks. No user Data or original private sample was modified or uploaded; ReadDock identifiers and legacy fixture bytes are retained.
