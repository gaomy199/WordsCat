param(
    [ValidateSet('win-x64')][string]$Runtime = 'win-x64',
    [string]$OutputDirectory = '',
    [switch]$Force,
    [switch]$Offline,
    [switch]$RequireClean
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if ($RequireClean) {
    $taskInitialChanges = @(git -C $taskRoot status --porcelain)
    if ($LASTEXITCODE -ne 0 -or $taskInitialChanges.Count -gt 0) { throw 'Release publication requires a clean source checkout.' }
}
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $taskRoot 'dist' }
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
$taskProject = [xml](Get-Content -LiteralPath (Join-Path $taskRoot 'src/WordsCat/WordsCat.csproj') -Raw)
$taskVersion = [string]$taskProject.Project.PropertyGroup.Version
if ($taskVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a three-part release version.' }
$taskName = 'WordsCat-' + $taskVersion + '-' + $Runtime + '.zip'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskZip = Join-Path $taskOutput $taskName
if ((Test-Path -LiteralPath $taskZip) -and -not $Force) { throw ('Package already exists; use -Force to replace this generated archive: ' + $taskZip) }

# A unique fresh directory prevents existing Data, previews and old output from
# entering a release. Never package an in-use publish/ directory.
$taskStage = Join-Path $taskRoot ('.build/release-' + [Guid]::NewGuid().ToString('N'))
$taskApp = Join-Path $taskStage 'WordsCat'
New-Item -ItemType Directory -Path $taskApp -Force | Out-Null
& (Join-Path $taskRoot 'build.ps1') -OutputPath $taskApp -SelfContained -Runtime $Runtime -Offline:$Offline
Copy-Item -LiteralPath (Join-Path $taskRoot 'THIRD_PARTY_NOTICES.md') -Destination $taskApp
foreach ($taskLegal in @('LICENSE','NOTICE','SECURITY.md')) {
    Copy-Item -LiteralPath (Join-Path $taskRoot $taskLegal) -Destination $taskApp
}
$taskNoticeFile = Join-Path $taskApp 'THIRD_PARTY_NOTICES.md'
[IO.File]::WriteAllText($taskNoticeFile, ([IO.File]::ReadAllText($taskNoticeFile).Replace('assets/licenses/', 'licenses/')), [Text.UTF8Encoding]::new($false))
$taskStart = @'
WordsCat portable for Windows 11 x64

1. Extract the complete ZIP to a writable folder.
2. Open the WordsCat folder and double-click WordsCat.exe.
3. Open Settings to configure your API key and Markdown notebooks.

The .NET runtime is included. You do not need the SDK or a separate .NET install.
Formula reading uses Microsoft Edge WebView2 Runtime. Text views remain available
if it is missing. Keep assets/math, ocr.ps1 and vocabulary.template.json together.

Upgrading: exit the old app first. Preserve your existing Data folder and notebooks.
Read USER_GUIDE.md or USER_GUIDE.zh-CN.md for the complete instructions.

https://github.com/gaomy199/WordsCat/releases/latest
'@
[IO.File]::WriteAllText((Join-Path $taskApp 'START_HERE.txt'), $taskStart + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
$taskRevision = git -C $taskRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot read the source revision.' }
$taskDirty = @(git -C $taskRoot status --porcelain).Count -gt 0
if ($LASTEXITCODE -ne 0) { throw 'Cannot check the source working tree.' }
$taskConfig = Get-Content -LiteralPath (Join-Path $taskApp 'WordsCat.runtimeconfig.json') -Raw | ConvertFrom-Json
$taskAssets = Get-Content -LiteralPath (Join-Path $taskRoot 'src/WordsCat/obj/project.assets.json') -Raw | ConvertFrom-Json
$taskRuntimeNotices = Join-Path $taskApp 'licenses/dotnet'
New-Item -ItemType Directory -Path $taskRuntimeNotices -Force | Out-Null
foreach ($taskFramework in $taskConfig.runtimeOptions.includedFrameworks) {
    $taskPack = switch ($taskFramework.name) {
        'Microsoft.NETCore.App' { 'microsoft.netcore.app.runtime.' + $Runtime }
        'Microsoft.WindowsDesktop.App' { 'microsoft.windowsdesktop.app.runtime.' + $Runtime }
        default { throw 'Unexpected bundled framework; its redistribution notices must be reviewed.' }
    }
    $taskPackRoot = $null
    foreach ($taskCache in $taskAssets.packageFolders.PSObject.Properties.Name) {
        $taskCandidate = Join-Path $taskCache ($taskPack + '/' + $taskFramework.version)
        if (Test-Path -LiteralPath $taskCandidate -PathType Container) { $taskPackRoot = $taskCandidate; break }
    }
    if (-not $taskPackRoot) { throw 'Cannot locate bundled runtime license metadata in restored packages.' }
    $taskPackLicense = @('LICENSE.TXT','LICENSE') | ForEach-Object { Join-Path $taskPackRoot $_ } | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (-not $taskPackLicense) { throw 'Missing bundled runtime license.' }
    Copy-Item -LiteralPath $taskPackLicense -Destination (Join-Path $taskRuntimeNotices ($taskFramework.name + '.LICENSE.txt'))
    $taskPackNotices = Join-Path $taskPackRoot 'THIRD-PARTY-NOTICES.TXT'
    if (Test-Path -LiteralPath $taskPackNotices) {
        Copy-Item -LiteralPath $taskPackNotices -Destination (Join-Path $taskRuntimeNotices ($taskFramework.name + '.THIRD-PARTY-NOTICES.txt'))
    }
    elseif ($taskFramework.name -eq 'Microsoft.NETCore.App') { throw 'Missing .NET runtime third-party notices.' }
}
$taskInfo = [ordered]@{ version=$taskVersion; runtime=$Runtime; selfContained=$true; frameworks=$taskConfig.runtimeOptions.includedFrameworks; sourceRevision=$taskRevision; sourceDirty=$taskDirty }
[IO.File]::WriteAllText((Join-Path $taskApp 'release-info.json'), ($taskInfo | ConvertTo-Json) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
$taskRequired = @('WordsCat.exe','WordsCat.dll','coreclr.dll','hostfxr.dll','System.Private.CoreLib.dll','System.Windows.Forms.dll','ocr.ps1','vocabulary.template.json','assets/math/reader.html','assets/math/fonts/KaTeX_Main-Regular.woff2','USER_GUIDE.md','USER_GUIDE.zh-CN.md','licenses/Microsoft.Web.WebView2.LICENSE.txt')
$taskRequired += @('LICENSE','NOTICE','SECURITY.md','THIRD_PARTY_NOTICES.md','assets/math/LICENSE','licenses/dotnet/Microsoft.NETCore.App.LICENSE.txt','licenses/dotnet/Microsoft.NETCore.App.THIRD-PARTY-NOTICES.txt','licenses/dotnet/Microsoft.WindowsDesktop.App.LICENSE.txt')
foreach ($taskFile in $taskRequired) { if (-not (Test-Path -LiteralPath (Join-Path $taskApp $taskFile) -PathType Leaf)) { throw ('Missing release dependency: ' + $taskFile) } }
if (-not $taskConfig.runtimeOptions.includedFrameworks -or $taskConfig.runtimeOptions.framework -or $taskConfig.runtimeOptions.frameworks) { throw 'The release must bundle its .NET frameworks.' }
$taskFiles = @(Get-ChildItem -LiteralPath $taskApp -Recurse -File)
foreach ($taskFile in $taskFiles) {
    $taskRelative = $taskFile.FullName.Substring($taskApp.Length + 1).Replace('\','/')
    if ($taskRelative -match '(^|/)(Data|PreviewData|Cache|fixtures|\.git|\.test-results)(/|$)|(^|/)(settings\.json|\.env(?:\..*)?)$|\.(pfx|pem|key|p12|p8|bak|log|tmp|pdb)$') { throw ('Private/generated file in release: ' + $taskRelative) }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskTempZip = Join-Path $taskStage $taskName
[IO.Compression.ZipFile]::CreateFromDirectory($taskApp, $taskTempZip, [IO.Compression.CompressionLevel]::Optimal, $true)
$taskArchive = [IO.Compression.ZipFile]::OpenRead($taskTempZip)
try {
    $taskEntries = @($taskArchive.Entries | Where-Object { $_.Name })
    if ($taskEntries.Count -ne $taskFiles.Count -or -not ($taskEntries.FullName -contains 'WordsCat/WordsCat.exe')) { throw 'Archive contents differ from the verified staging directory.' }
    if ($taskEntries.FullName | Where-Object { $_ -match '(^|/)(Data|PreviewData|Cache|fixtures|\.git)(/|$)' }) { throw 'Private directory in archive.' }
}
finally { $taskArchive.Dispose() }
Move-Item -LiteralPath $taskTempZip -Destination $taskZip -Force
$taskHash = (Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $taskOutput 'SHA256SUMS.txt'), ($taskHash + '  ' + $taskName + "`n"), [Text.UTF8Encoding]::new($false))
Write-Output ('Archive: ' + $taskZip)
Write-Output ('SHA256: ' + $taskHash)
Write-Output ('Verified staging directory: ' + $taskApp)
Write-Output ('Files: ' + $taskFiles.Count)
