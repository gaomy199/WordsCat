# Third-party notices

## KaTeX 0.16.22

Bundled JavaScript, CSS, and fonts under `assets/math/` come from the official KaTeX package. Copyright (c) 2013–2020 Khan Academy and other contributors. Distributed under the MIT License; the complete notice is preserved in [assets/math/LICENSE](assets/math/LICENSE).

`reader.html` and `reader.js` are application-owned integration files rather than files from the KaTeX distribution.

## Microsoft.Web.WebView2 1.0.3595.46

The .NET project references Microsoft's WebView2 SDK through NuGet. Copyright Microsoft Corporation. The package's original license is preserved in [assets/licenses/Microsoft.Web.WebView2.LICENSE.txt](assets/licenses/Microsoft.Web.WebView2.LICENSE.txt) and copied into the portable output.

The installed Microsoft Edge WebView2 Runtime is a separate runtime prerequisite and is not bundled. Times New Roman and other operating-system fonts are used when installed; their files are not redistributed.

## Bundled .NET runtime

Self-contained Windows ZIPs include Microsoft.NETCore.App and Microsoft.WindowsDesktop.App, with exact versions recorded in `release-info.json`. The .NET runtime and Windows Desktop framework are licensed under MIT, copyright .NET Foundation and Contributors. The packager preserves the resolved runtime packages' original licenses under `licenses/dotnet/` and the .NET runtime's complete `THIRD-PARTY-NOTICES.TXT` there. These notices cover the runtime's own bundled third-party components. Missing runtime notices fail packaging. Framework-dependent developer builds instead use the installed .NET Windows Desktop Runtime.

Upstream: [.NET runtime license](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT), [.NET runtime third-party notices](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT), [Windows Desktop license](https://github.com/dotnet/windowsdesktop/blob/main/LICENSE).

## WordsCat-owned files

WordsCat source, integration files and original application assets are distributed under Apache-2.0; see LICENSE and NOTICE. The project license does not replace any third-party license above. The user-provided default icon is included as an application asset; no operating-system icons or fonts are copied into the archive.
