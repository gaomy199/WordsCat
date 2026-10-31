$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskScratch = Join-Path $taskRoot ('.test-results/secret-guard-negative-' + [Guid]::NewGuid().ToString('N'))
$taskExpectedRoot = [IO.Path]::GetFullPath((Join-Path $taskRoot '.test-results')) + [IO.Path]::DirectorySeparatorChar
if (-not [IO.Path]::GetFullPath($taskScratch).StartsWith($taskExpectedRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test directory.' }
New-Item -ItemType Directory -Path $taskScratch -Force | Out-Null
$taskEngine = (Get-Process -Id $PID).Path
function Invoke-NegativeGuard {
    $taskStart = [Diagnostics.ProcessStartInfo]::new($taskEngine)
    $taskStart.Arguments = '-NoLogo -NoProfile -NonInteractive -File "' + (Join-Path $PSScriptRoot 'check-secrets.ps1') + '" -Workspace "' + $taskScratch + '"'
    $taskStart.UseShellExecute = $false; $taskStart.CreateNoWindow = $true
    $taskStart.RedirectStandardOutput = $true; $taskStart.RedirectStandardError = $true
    $taskProcess = [Diagnostics.Process]::Start($taskStart)
    try {
        $taskOutput = $taskProcess.StandardOutput.ReadToEnd(); $taskErrors = $taskProcess.StandardError.ReadToEnd(); $taskProcess.WaitForExit()
        if ($taskProcess.ExitCode -ne 1 -or $taskErrors -or $taskOutput.Contains($taskProbe)) { throw 'Negative guard failed without safe diagnostics.' }
        foreach ($taskRow in ($taskOutput -split '\r?\n' | Where-Object { $_ })) { if ($taskRow -notmatch '^[^\r\n]+:\d+$') { throw 'Guard emitted more than a location.' } }
        return $taskOutput
    }
    finally { $taskProcess.Dispose() }
}
try {
    $taskProbe = [string]::Concat('s','k','-') + ('x' * 32)
    $taskCase = Join-Path $taskScratch 'probe.ps1'
    $taskPayload = '$apiKey = "' + $taskProbe + '"' + "`n" + ('Authorization' + ': Bearer ' + $taskProbe)
    [IO.File]::WriteAllText($taskCase,$taskPayload,[Text.UTF8Encoding]::new($false))
    if ((Invoke-NegativeGuard) -notmatch 'probe.ps1:1') { throw 'Worktree negative probe was not detected.' }
    [IO.File]::WriteAllText($taskCase,$taskPayload,[Text.Encoding]::Unicode)
    if ((Invoke-NegativeGuard) -notmatch 'probe.ps1:1') { throw 'UTF-16 negative probe was not detected.' }
    [IO.File]::WriteAllBytes((Join-Path $taskScratch 'compiled.dll'),[Text.Encoding]::Unicode.GetBytes($taskProbe))
    if ((Invoke-NegativeGuard) -notmatch 'compiled.dll:1') { throw 'Compiled-string negative probe was not detected.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $taskZip = [IO.Compression.ZipFile]::Open((Join-Path $taskScratch 'archive.zip'),[IO.Compression.ZipArchiveMode]::Create)
    try {
        $taskZipEntry = $taskZip.CreateEntry('probe.txt'); $taskZipStream = $taskZipEntry.Open()
        try { $taskZipBytes=[Text.Encoding]::UTF8.GetBytes($taskPayload); $taskZipStream.Write($taskZipBytes,0,$taskZipBytes.Length) }
        finally { $taskZipStream.Dispose() }
    }
    finally { $taskZip.Dispose() }
    if ((Invoke-NegativeGuard) -notmatch 'archive.zip!probe.txt:1') { throw 'Archive negative probe was not detected.' }
    git -C $taskScratch init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize isolated index test.' }
    git -C $taskScratch add -- probe.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Cannot stage isolated negative probe.' }
    [IO.File]::WriteAllText($taskCase,'# clean working copy',[Text.UTF8Encoding]::new($false))
    if ((Invoke-NegativeGuard) -notmatch 'staged/probe.ps1:1') { throw 'Staged secret was hidden by a clean working copy.' }
    Write-Output 'PASS: UTF-8, UTF-16, compiled strings, ZIP entries and staged-only negative probes fail safely; output contains locations only.'
}
finally {
    # Only this verified, uniquely named disposable test directory is removed.
    if ([IO.Path]::GetFullPath($taskScratch).StartsWith($taskExpectedRoot,[StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $taskScratch -Recurse -Force }
}
