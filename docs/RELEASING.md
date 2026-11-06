# Publishing a portable release

The supported binary target is Windows 11 x64 (`win-x64`). The application uses Windows Forms, Windows OCR, DPAPI and other Windows APIs; this is not a macOS or Linux build.

## Build

Update the application version, visible version badge, self-test report, guides and changelog together. Build with the .NET 10 SDK. The project targets .NET 10 (LTS) and uses the runtime patch selected by that SDK for self-contained publishing; check the included framework versions in `release-info.json` before distribution. Build and actual-window regression checks pass on SDK 10.0.403 / Windows Desktop Runtime 10.0.14; see [VALIDATION-v0.3.4.md](VALIDATION-v0.3.4.md).

From the repository root on Windows:

```powershell
.\scripts\check-source.ps1
.\scripts\publish-release.ps1
```

The packager builds into a new `.build/release-GUID/WordsCat/app/` directory rather than reusing `publish/`. It bundles the .NET runtime, formula resources, OCR helper, template, guides and third-party notices there. The outer `WordsCat/` contains only the native `WordsCat.exe` launcher, `START_HERE.txt` and `app/`. Trimming is disabled for the Windows Forms application. User Data is created beside the outer launcher and is excluded from the ZIP.

Building the native launcher requires x64 MinGW-w64 (GCC and windres) or Visual Studio C++ Build Tools with a Windows SDK. The local machine can use its installed MinGW; the script also locates the Visual Studio toolchain on GitHub's Windows runner. End users need neither compiler nor separately installed .NET. The launcher only starts the C# application, forwards arguments and waits/returns its exit code, so `verify.ps1 -PublishPath <extracted WordsCat>` continues to exercise the packaged entry point.

`publish-release.ps1` is the public packaging entry point; `package-release.ps1` remains the shared implementation and compatibility entry point. Both accept `-Offline` for cached dependencies and `-RequireClean` to reject uncommitted source. The release workflow requires a clean checkout. LICENSE, NOTICE, SECURITY.md, KaTeX/WebView2 notices, and the actual resolved runtime-pack license/third-party files are required in each self-contained ZIP.

The packager supports Windows PowerShell 5.1 and PowerShell 7. It writes explicit forward-slash ZIP entry names and checks the complete file-name set against staging. The older .NET Framework `CreateFromDirectory` implementation could write backslashes, causing the previous forward-slash executable check to fail even when all files were present. Do not bypass archive verification to work around that error.

The output is `dist/WordsCat-VERSION-win-x64.zip` plus `dist/SHA256SUMS.txt`. The ZIP has a top-level `WordsCat/` folder. `app/release-info.json` records the application version, architecture, layout, included frameworks, source revision, and whether source changes were uncommitted. Publish only a build with `sourceDirty: false`. `-Force` replaces only the generated ZIP for that version; it does not clean user data directories.

Do not include a fixed WebView2 browser or silently install system components. The app uses the installed Edge WebView2 Runtime and retains text views if it is unavailable.

The latest locally validated archive is `dist/WordsCat-0.3.9-win-x64.zip`; evidence is in [VALIDATION-v0.3.9.md](VALIDATION-v0.3.9.md). It is an uncommitted working-tree package (`sourceDirty: true`), not a published GitHub Release. Previous archives are retained.

On 2026-11-06, development packages of 0.3.18 were created with both Windows PowerShell 5.1 and PowerShell 7 under `.test-results/package-compat-0.3.18-1dca1320b64e48fcb3e577fe7f141dba/`. Both contain 357 files with standard paths, byte-for-byte staging matches and matching checksum files. These compatibility-test packages have `sourceDirty: true`; rebuild from the final clean commit for publication. They do not replace the older `dist/` archives.

## Verify the actual archive

Extract to a new directory, then run:

```powershell
.\verify.ps1 -PublishPath '.test-results\release-extracted\WordsCat' -IncludeUi
```

Check `app/WordsCat.runtimeconfig.json` contains `includedFrameworks`, and that `app/` includes `coreclr.dll`, `hostfxr.dll`, `System.Private.CoreLib.dll` and Windows Forms libraries. To check the runtime is not resolving an installed shared framework, run the extracted self-test with an isolated empty `DOTNET_ROOT`, host tracing, and synthetic `--data-dir`; the trace must resolve the included frameworks inside the extracted app folder.

Open the extracted executable through `--offline-preview --data-dir <isolated directory>` and inspect it with Computer Use. Verify the archive has no `Data/`, settings, credentials, screenshots, history, caches, test results, or debugging symbols. Never run a preview using real credentials during release validation.

For 0.4.1, the implementation task deliberately runs builds only at the user's request. No launcher execution, automated tests, archive or release is performed in that task. Before publishing, the maintainer should verify both the app and native entry point, move an extracted copy to a different directory, and check outer Data creation using synthetic/isolated data. The MinGW build result does not establish the unexecuted Visual Studio build path or hosted workflow result.

## Maintainer command order for 0.4.1

Run each block only after the previous block succeeds. These are maintainer instructions, not actions already executed by the agent.

```powershell
cd "C:\Users\lenovo\Documents\Codex\Projects\ReadDock"
.\build.ps1
.\verify.ps1 -IncludeUi
.\scripts\check-source.ps1
git diff --check
git config core.hooksPath .githooks
git add .
.\scripts\check-secrets.ps1
git commit -m "Release WordsCat 0.4.1"
.\scripts\publish-release.ps1 -RequireClean
```

Extract and verify the actual ZIP into a new isolated directory:

```powershell
$taskReleaseCheck = Join-Path $PWD ('.test-results/release-0.4.1-' + [Guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath .\dist\WordsCat-0.4.1-win-x64.zip -DestinationPath $taskReleaseCheck
.\verify.ps1 -PublishPath (Join-Path $taskReleaseCheck 'WordsCat') -IncludeUi
.\scripts\check-source.ps1
```

The root should show `WordsCat.exe`, `START_HERE.txt` and `app/`, with no user Data in the downloaded package. For a manual offline preview, pass an explicit synthetic data directory to the outer launcher. After checks pass, push the source and new version tag to trigger the existing automatic Release job:

```powershell
git push origin main
git tag v0.4.1
git push origin v0.4.1
```

Watch the Release workflow under GitHub Actions; a successful job creates the Release with ZIP/checksum attachments. Do not separately create the same release while that workflow is running. If `v0.4.1` already exists, stop and inspect its target instead of force-updating it. The hosted job retains its own verification steps; the user's request to skip local agent tests does not disable CI.

## Publish

When source submission/publication has been authorized, commit and push the reviewed source, then tag the exact build commit as `vVERSION`. `.github/workflows/release.yml` handles matching `v*` tag pushes: compare the tag with the project version, build and verify without UI, package from a clean checkout, extract/retest, scan secrets, and create a GitHub Release with the named ZIP and `SHA256SUMS.txt`. It uses the built-in GitHub token with contents-write permission only in the release job; checkout does not persist credentials. It refuses an existing release rather than overwriting assets. Keep binaries in Release assets, never Git history.

If a packaging fix is committed after a local tag was created, update that tag to the final commit before pushing it. Check GitHub first: `git tag -f vVERSION` changes only the local tag and is appropriate for a tag that has not been pushed. Do not force-push or silently replace a published release tag. Run local `publish-release.ps1 -RequireClean` only after all source and documentation changes are committed.

The CI workflow runs on main pushes and pull requests with read-only contents permission. Hosted workflows cannot be executed locally: static validation and local equivalents do not prove GitHub runner/authentication behavior. Observe the first hosted CI and tag run after submission. Before a public release, enable GitHub private vulnerability reporting so the SECURITY.md link is usable. This task does not change repository settings or publish anything.

The application is unsigned. Release notes and README must mention possible SmartScreen warnings and show `Get-FileHash <archive> -Algorithm SHA256` compared with the matching checksum line. Do not advise users to disable security protections. A checksum is an integrity check, not a signing certificate.

The release notes should identify Windows x64, explain extract-and-run, state that .NET is bundled, mention WebView2/text fallback, and explain preservation of `Data/` when upgrading. Link users to the named Windows ZIP rather than GitHub's source archives.

References: [Microsoft self-contained publishing](https://learn.microsoft.com/en-us/dotnet/core/deploying/), [GitHub Releases API](https://docs.github.com/en/rest/releases/releases).
