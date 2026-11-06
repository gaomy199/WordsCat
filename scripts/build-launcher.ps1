param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskOutput = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path (Split-Path -Parent $taskOutput) -Force | Out-Null
$taskBuild = Join-Path $taskRoot ('.build/launcher-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskBuild -Force | Out-Null
$taskProject = [xml](Get-Content -LiteralPath (Join-Path $taskRoot 'src/WordsCat/WordsCat.csproj') -Raw)
$taskVersion = [string]$taskProject.Project.PropertyGroup.Version
if ($taskVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a three-part launcher version.' }
$taskVersionNumbers = $taskVersion.Replace('.', ',') + ',0'
$taskIcon = (Join-Path $taskRoot 'assets/wordscat.ico').Replace('\','/')
$taskManifest = (Join-Path $taskRoot 'src/WordsCat/app.manifest').Replace('\','/')
$taskResource = Join-Path $taskBuild 'launcher.rc'
$taskResourceText = @"
#include <windows.h>
1 ICON "$taskIcon"
1 RT_MANIFEST "$taskManifest"
1 VERSIONINFO
FILEVERSION $taskVersionNumbers
PRODUCTVERSION $taskVersionNumbers
FILEOS 0x40004L
FILETYPE 0x1L
BEGIN
 BLOCK "StringFileInfo"
 BEGIN
  BLOCK "040904b0"
  BEGIN
   VALUE "CompanyName", "WordsCat\0"
   VALUE "FileDescription", "WordsCat portable launcher\0"
   VALUE "FileVersion", "$taskVersion\0"
   VALUE "ProductName", "WordsCat\0"
   VALUE "ProductVersion", "$taskVersion\0"
   VALUE "OriginalFilename", "WordsCat.exe\0"
  END
 END
 BLOCK "VarFileInfo"
 BEGIN
  VALUE "Translation", 0x409, 1200
 END
END
"@
[IO.File]::WriteAllText($taskResource, $taskResourceText, [Text.UTF8Encoding]::new($false))
$taskSource = Join-Path $taskRoot 'src/Launcher/launcher.c'
$taskGcc = Get-Command gcc.exe -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $taskGcc -and (Test-Path -LiteralPath 'C:/msys64/mingw64/bin/gcc.exe')) {
    $taskGcc = Get-Item -LiteralPath 'C:/msys64/mingw64/bin/gcc.exe'
    $taskGccPath = $taskGcc.FullName
} elseif ($taskGcc) { $taskGccPath = $taskGcc.Source }
if ($taskGcc) {
    $taskMachine = & $taskGccPath -dumpmachine
    if ($LASTEXITCODE -ne 0 -or $taskMachine -notmatch '^x86_64-w64-mingw32') { throw 'The launcher requires a Windows x64 MinGW compiler.' }
    $taskWindres = Join-Path (Split-Path -Parent $taskGccPath) 'windres.exe'
    if (-not (Test-Path -LiteralPath $taskWindres)) {
        $taskWindresCommand = Get-Command windres.exe -ErrorAction Stop | Select-Object -First 1
        $taskWindres = $taskWindresCommand.Source
    }
    $taskObject = Join-Path $taskBuild 'launcher-resource.o'
    & $taskWindres '--target=pe-x86-64' '-i' $taskResource '-o' $taskObject '-O' 'coff'
    if ($LASTEXITCODE -ne 0) { throw 'Launcher resource compilation failed.' }
    & $taskGccPath '-Os' '-fno-stack-protector' '-ffreestanding' '-fno-builtin' '-nostdlib' '-mwindows' '-Wl,-e,LauncherMain' '-Wl,--dynamicbase,--nxcompat,--high-entropy-va' $taskSource $taskObject '-lkernel32' '-luser32' '-o' $taskOutput
    if ($LASTEXITCODE -ne 0) { throw 'Launcher compilation failed.' }
} else {
    # GitHub's Windows runner includes Visual Studio and the Windows SDK.
    # Locate explicit compiler/header/library paths; do not capture shell environments.
    $taskVswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (-not (Test-Path -LiteralPath $taskVswhere)) { throw 'Install x64 MinGW-w64 or Visual Studio C++ Build Tools to build the portable launcher.' }
    $taskVs = & $taskVswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if ($LASTEXITCODE -ne 0 -or -not $taskVs) { throw 'Visual Studio x64 C++ tools were not found.' }
    $taskVcVersion = ([IO.File]::ReadAllText((Join-Path $taskVs 'VC/Auxiliary/Build/Microsoft.VCToolsVersion.default.txt'))).Trim()
    $taskVc = Join-Path $taskVs ('VC/Tools/MSVC/' + $taskVcVersion)
    $taskSdk = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
    $taskSdkVersion = Get-ChildItem -LiteralPath (Join-Path $taskSdk 'Lib') -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'um/x64/kernel32.lib') } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if (-not $taskSdkVersion) { throw 'Windows x64 SDK libraries were not found.' }
    $taskSdkInclude = Join-Path $taskSdk ('Include/' + $taskSdkVersion.Name)
    $taskRc = Join-Path $taskSdk ('bin/' + $taskSdkVersion.Name + '/x64/rc.exe')
    $taskRes = Join-Path $taskBuild 'launcher.res'
    $taskIncludes = @(('/I' + (Join-Path $taskVc 'include')), ('/I' + (Join-Path $taskSdkInclude 'shared')), ('/I' + (Join-Path $taskSdkInclude 'um')), ('/I' + (Join-Path $taskSdkInclude 'ucrt')))
    & $taskRc /nologo @taskIncludes (('/fo') + $taskRes) $taskResource
    if ($LASTEXITCODE -ne 0) { throw 'Launcher resource compilation failed.' }
    $taskCl = Join-Path $taskVc 'bin/Hostx64/x64/cl.exe'
    & $taskCl /nologo /O1 /GS- /TC @taskIncludes (('/Fo') + (Join-Path $taskBuild 'launcher.obj')) $taskSource $taskRes /link /NODEFAULTLIB /ENTRY:LauncherMain /SUBSYSTEM:WINDOWS /MACHINE:X64 /DYNAMICBASE /NXCOMPAT /HIGHENTROPYVA /OPT:REF /OPT:ICF (('/LIBPATH:') + (Join-Path $taskSdkVersion.FullName 'um/x64')) kernel32.lib user32.lib (('/OUT:') + $taskOutput)
    if ($LASTEXITCODE -ne 0) { throw 'Launcher compilation failed.' }
}
Write-Output ('Launcher: ' + $taskOutput)
