# Publishing a portable release

The supported binary target is Windows 11 x64 (`win-x64`). The application uses Windows Forms, Windows OCR, DPAPI and other Windows APIs; this is not a macOS or Linux build.

## Build

Update the application version, visible version badge, self-test report, guides and changelog together. The self-contained runtime patch is pinned in the project file; review Microsoft's .NET 8 release metadata before changing that pin.

From the repository root on Windows:

```powershell
.\scripts\check-source.ps1
.\scripts\package-release.ps1
```

The packager builds into a new `.build/release-GUID/WordsCat/` directory rather than reusing `publish/`. It bundles the .NET runtime, formula resources, OCR helper, template, guides, third-party notices, and `START_HERE.txt`. Trimming is disabled for the Windows Forms application.

The output is `dist/WordsCat-VERSION-win-x64.zip` plus `dist/SHA256SUMS.txt`. The ZIP has a top-level `WordsCat/` folder. `release-info.json` records the application version, architecture, included frameworks, source revision, and whether source changes were uncommitted. Publish only a build with `sourceDirty: false`. `-Force` replaces only the generated ZIP for that version; it does not clean user data directories.

Do not include a fixed WebView2 browser or silently install system components. The app uses the installed Edge WebView2 Runtime and retains text views if it is unavailable.

## Verify the actual archive

Extract to a new directory, then run:

```powershell
.\verify.ps1 -PublishPath '.test-results\release-extracted\WordsCat' -IncludeUi
```

Check `WordsCat.runtimeconfig.json` contains `includedFrameworks`, and that the extracted folder includes `coreclr.dll`, `hostfxr.dll`, `System.Private.CoreLib.dll` and Windows Forms libraries. To check the runtime is not resolving an installed shared framework, run the extracted self-test with an isolated empty `DOTNET_ROOT`, host tracing, and synthetic `--data-dir`; the trace must resolve the included frameworks to the extracted folder.

Open the extracted executable through `--offline-preview --data-dir <isolated directory>` and inspect it with Computer Use. Verify the archive has no `Data/`, settings, credentials, screenshots, history, caches, test results, or debugging symbols. Never run a preview using real credentials during release validation.

## Publish

Commit and push the source, then tag the exact build commit as `vVERSION`. Rebuild the final archive from that clean commit so its embedded revision matches the release tag. Create a GitHub Release for that tag, upload the named Windows ZIP and `SHA256SUMS.txt`, and publish it after checking the uploaded sizes and SHA-256 digests. Keep binaries in Release assets rather than Git history. Do not overwrite an existing published version's assets.

The release notes should identify Windows x64, explain extract-and-run, state that .NET is bundled, mention WebView2/text fallback, and explain preservation of `Data/` when upgrading. Link users to the named Windows ZIP rather than GitHub's source archives.

References: [Microsoft self-contained publishing](https://learn.microsoft.com/en-us/dotnet/core/deploying/), [GitHub Releases API](https://docs.github.com/en/rest/releases/releases).
