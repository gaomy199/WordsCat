# Publishing a portable release

The supported binary target is Windows 11 x64 (`win-x64`). The application uses Windows Forms, Windows OCR, DPAPI and other Windows APIs; this is not a macOS or Linux build.

## Build

Update the application version, visible version badge, self-test report, guides and changelog together. Build with the .NET 10 SDK. The project targets .NET 10 (LTS) and uses the runtime patch selected by that SDK for self-contained publishing; check the included framework versions in `release-info.json` before distribution. Build and actual-window regression checks pass on SDK 10.0.403 / Windows Desktop Runtime 10.0.14; see [VALIDATION-v0.3.4.md](VALIDATION-v0.3.4.md).

From the repository root on Windows:

```powershell
.\scripts\check-source.ps1
.\scripts\publish-release.ps1
```

The packager builds into a new `.build/release-GUID/WordsCat/` directory rather than reusing `publish/`. It bundles the .NET runtime, formula resources, OCR helper, template, guides, third-party notices, and `START_HERE.txt`. Trimming is disabled for the Windows Forms application.

`publish-release.ps1` is the public packaging entry point; `package-release.ps1` remains the shared implementation and compatibility entry point. Both accept `-Offline` for cached dependencies and `-RequireClean` to reject uncommitted source. The release workflow requires a clean checkout. LICENSE, NOTICE, SECURITY.md, KaTeX/WebView2 notices, and the actual resolved runtime-pack license/third-party files are required in each self-contained ZIP.

The output is `dist/WordsCat-VERSION-win-x64.zip` plus `dist/SHA256SUMS.txt`. The ZIP has a top-level `WordsCat/` folder. `release-info.json` records the application version, architecture, included frameworks, source revision, and whether source changes were uncommitted. Publish only a build with `sourceDirty: false`. `-Force` replaces only the generated ZIP for that version; it does not clean user data directories.

Do not include a fixed WebView2 browser or silently install system components. The app uses the installed Edge WebView2 Runtime and retains text views if it is unavailable.

The locally validated 0.3.7 archive is `dist/WordsCat-0.3.7-win-x64.zip`; evidence is in [VALIDATION-v0.3.7.md](VALIDATION-v0.3.7.md). It is an uncommitted working-tree package (`sourceDirty: true`), not a published GitHub Release. Previous archives are retained.

## Verify the actual archive

Extract to a new directory, then run:

```powershell
.\verify.ps1 -PublishPath '.test-results\release-extracted\WordsCat' -IncludeUi
```

Check `WordsCat.runtimeconfig.json` contains `includedFrameworks`, and that the extracted folder includes `coreclr.dll`, `hostfxr.dll`, `System.Private.CoreLib.dll` and Windows Forms libraries. To check the runtime is not resolving an installed shared framework, run the extracted self-test with an isolated empty `DOTNET_ROOT`, host tracing, and synthetic `--data-dir`; the trace must resolve the included frameworks to the extracted folder.

Open the extracted executable through `--offline-preview --data-dir <isolated directory>` and inspect it with Computer Use. Verify the archive has no `Data/`, settings, credentials, screenshots, history, caches, test results, or debugging symbols. Never run a preview using real credentials during release validation.

## Publish

When source submission/publication has been authorized, commit and push the reviewed source, then tag the exact build commit as `vVERSION`. `.github/workflows/release.yml` handles matching `v*` tag pushes: compare the tag with the project version, build and verify without UI, package from a clean checkout, extract/retest, scan secrets, and create a GitHub Release with the named ZIP and `SHA256SUMS.txt`. It uses the built-in GitHub token with contents-write permission only in the release job; checkout does not persist credentials. It refuses an existing release rather than overwriting assets. Keep binaries in Release assets, never Git history.

The CI workflow runs on main pushes and pull requests with read-only contents permission. Hosted workflows cannot be executed locally: static validation and local equivalents do not prove GitHub runner/authentication behavior. Observe the first hosted CI and tag run after submission. Before a public release, enable GitHub private vulnerability reporting so the SECURITY.md link is usable. This task does not change repository settings or publish anything.

The application is unsigned. Release notes and README must mention possible SmartScreen warnings and show `Get-FileHash <archive> -Algorithm SHA256` compared with the matching checksum line. Do not advise users to disable security protections. A checksum is an integrity check, not a signing certificate.

The release notes should identify Windows x64, explain extract-and-run, state that .NET is bundled, mention WebView2/text fallback, and explain preservation of `Data/` when upgrading. Link users to the named Windows ZIP rather than GitHub's source archives.

References: [Microsoft self-contained publishing](https://learn.microsoft.com/en-us/dotnet/core/deploying/), [GitHub Releases API](https://docs.github.com/en/rest/releases/releases).
