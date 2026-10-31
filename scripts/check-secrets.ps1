param(
    [string]$Workspace = (Split-Path -Parent $PSScriptRoot),
    [switch]$IncludeTypes
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($Workspace).TrimEnd('\','/')
$taskGuardSource = Join-Path (Split-Path -Parent $PSScriptRoot) 'src/WordsCat/Security/SecretRedactor.cs'
try {
    if (-not ('ReadDock.SecretRedactor' -as [type])) { Add-Type -Path $taskGuardSource -ErrorAction Stop 2>$null }
    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop 2>$null
}
catch { [Console]::Error.WriteLine('scripts/check-secrets.ps1:0'); exit 2 }
$taskHits = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$taskContentCache = @{}
$taskTextExtensions = @('.cs','.ps1','.psm1','.psd1','.md','.json','.jsonl','.csproj','.props','.targets','.config','.xml','.txt','.log','.yml','.yaml','.toml','.ini','.env','.sh','.bat','.cmd','.js','.html','.css','.nuspec','.sln','.slnx','.user','.secret','.bak','.tmp')
$taskArchiveExtensions = @('.zip','.nupkg')
function Add-GuardHit([string]$taskPath,[int]$taskLine,[string]$taskType) {
    $taskRow = $taskPath + ':' + $taskLine
    if ($IncludeTypes) { $taskRow += ':' + $taskType }
    [void]$taskHits.Add($taskRow)
}
function Read-GuardGit([string[]]$taskArguments) {
    $taskStart = [Diagnostics.ProcessStartInfo]::new('git')
    $taskStart.Arguments = '-C "' + $taskRoot.Replace('"','') + '" ' + ($taskArguments -join ' ')
    $taskStart.UseShellExecute = $false; $taskStart.CreateNoWindow = $true
    $taskStart.RedirectStandardOutput = $true; $taskStart.RedirectStandardError = $true
    $taskProcess = [Diagnostics.Process]::Start($taskStart)
    $taskMemory = [IO.MemoryStream]::new()
    try {
        $taskProcess.StandardOutput.BaseStream.CopyTo($taskMemory)
        $null = $taskProcess.StandardError.ReadToEnd(); $taskProcess.WaitForExit()
        if ($taskProcess.ExitCode -ne 0) { throw 'guard-git-failed' }
        return ,$taskMemory.ToArray()
    }
    finally { $taskMemory.Dispose(); $taskProcess.Dispose() }
}
function Test-GuardBytes([byte[]]$taskBytes,[string]$taskPath,[string]$taskExtension,[int]$taskDepth=0) {
    if ($taskExtension -in $taskArchiveExtensions) {
        if ($taskDepth -gt 4) { Add-GuardHit $taskPath 0 'archive-depth'; return }
        $taskMemory = [IO.MemoryStream]::new($taskBytes,$false)
        $taskArchive = $null
        try {
            $taskArchive = [IO.Compression.ZipArchive]::new($taskMemory,[IO.Compression.ZipArchiveMode]::Read)
            foreach ($taskEntry in $taskArchive.Entries) {
                if (-not $taskEntry.Name) { continue }
                if ($taskEntry.Length -gt 268435456) { Add-GuardHit ($taskPath+'!'+$taskEntry.FullName) 0 'entry-size'; continue }
                $taskEntryStream = $taskEntry.Open(); $taskEntryMemory = [IO.MemoryStream]::new()
                try { $taskEntryStream.CopyTo($taskEntryMemory); Test-GuardBytes $taskEntryMemory.ToArray() ($taskPath+'!'+$taskEntry.FullName) ([IO.Path]::GetExtension($taskEntry.Name).ToLowerInvariant()) ($taskDepth+1) }
                finally { $taskEntryStream.Dispose(); $taskEntryMemory.Dispose() }
            }
        }
        catch { Add-GuardHit $taskPath 0 'archive-read-error' }
        finally { if ($taskArchive) { $taskArchive.Dispose() }; $taskMemory.Dispose() }
        return
    }
    $taskIsBinary = $taskExtension -in @('.dll','.exe')
    if (-not $taskIsBinary -and $taskExtension -notin $taskTextExtensions) {
        for ($taskByteIndex=0; $taskByteIndex -lt [Math]::Min(4096,$taskBytes.Length); $taskByteIndex++) {
            if ($taskBytes[$taskByteIndex] -eq 0) { $taskIsBinary=$true; break }
        }
    }
    $taskSha = [Security.Cryptography.SHA256]::Create()
    try { $taskDigest = [Convert]::ToBase64String($taskSha.ComputeHash($taskBytes)) + ':' + $taskIsBinary }
    finally { $taskSha.Dispose() }
    if (-not $taskContentCache.ContainsKey($taskDigest)) {
        $taskMatches = [Collections.Generic.List[object]]::new()
        $taskTexts = @([Text.Encoding]::UTF8.GetString($taskBytes))
        if ($taskIsBinary -or ($taskBytes.Length -gt 1 -and ($taskBytes[0] -eq 255 -or $taskBytes[0] -eq 254 -or $taskBytes[1] -eq 0))) {
            $taskTexts += [Text.Encoding]::Unicode.GetString($taskBytes)
            if ($taskIsBinary -and $taskBytes.Length -gt 1) { $taskTexts += [Text.Encoding]::Unicode.GetString($taskBytes,1,$taskBytes.Length-1) }
            if (-not $taskIsBinary -and $taskBytes[0] -eq 254) { $taskTexts += [Text.Encoding]::BigEndianUnicode.GetString($taskBytes) }
        }
        foreach ($taskText in $taskTexts) {
            $taskDetected = if ($taskIsBinary) { [ReadDock.SecretRedactor]::FindBinary($taskText) } else { [ReadDock.SecretRedactor]::Find($taskText) }
            foreach ($taskMatch in $taskDetected) {
                $taskLine = if ($taskIsBinary) { 1 } else { 1 + [regex]::Matches($taskText.Substring(0,$taskMatch.Index),"`n").Count }
                $taskMatches.Add(@($taskLine,$taskMatch.Type))
            }
        }
        $taskContentCache[$taskDigest] = $taskMatches
    }
    foreach ($taskMatch in $taskContentCache[$taskDigest]) { Add-GuardHit $taskPath $taskMatch[0] $taskMatch[1] }
}
function Scan-GuardDirectory([string]$taskDirectory) {
    try { $taskEntries = [IO.Directory]::GetFileSystemEntries($taskDirectory) }
    catch { Add-GuardHit ($taskDirectory.Substring($taskRoot.Length).TrimStart('\','/').Replace('\','/')) 0 'directory-read-error'; return }
    foreach ($taskEntry in $taskEntries) {
        $taskRelative = $taskEntry.Substring($taskRoot.Length+1).Replace('\','/')
        if ([IO.Path]::GetFileName($taskEntry) -eq '.git') { continue } # Index is scanned separately; history is intentionally untouched.
        try {
            $taskAttributes = [IO.File]::GetAttributes($taskEntry)
            if ($taskAttributes -band [IO.FileAttributes]::ReparsePoint) { Add-GuardHit $taskRelative 0 'reparse-point'; continue }
            if ($taskAttributes -band [IO.FileAttributes]::Directory) { Scan-GuardDirectory $taskEntry; continue }
            Test-GuardBytes ([IO.File]::ReadAllBytes($taskEntry)) $taskRelative ([IO.Path]::GetExtension($taskEntry).ToLowerInvariant())
        }
        catch { Add-GuardHit $taskRelative 0 'file-read-error' }
    }
}
try {
    Scan-GuardDirectory $taskRoot
    # Read index blobs, not working-tree copies. A clean file on disk must not
    # hide a credential that is still staged. These commands never mutate Git.
    if (Test-Path -LiteralPath (Join-Path $taskRoot '.git')) {
        $taskIndex = [Text.Encoding]::UTF8.GetString((Read-GuardGit @('-c','core.quotepath=false','ls-files','--stage','-z')))
        foreach ($taskEntry in $taskIndex.Split([char]0)) {
            if (-not $taskEntry) { continue }
            if ($taskEntry -notmatch '^\d+ (?<blob>[a-f0-9]{40}) \d\t(?<path>.+)$') { Add-GuardHit 'staged-index' 0 'index-entry'; continue }
            $taskBlob = $Matches.blob; $taskPath = $Matches.path
            Test-GuardBytes (Read-GuardGit @('cat-file','blob',$taskBlob)) ('staged/'+$taskPath) ([IO.Path]::GetExtension($taskPath).ToLowerInvariant())
        }
    }
}
catch { Add-GuardHit 'scripts/check-secrets.ps1' 0 'scan-error' }
foreach ($taskHit in ($taskHits | Sort-Object)) { [Console]::Out.WriteLine($taskHit) }
if ($taskHits.Count) { exit 1 }
exit 0
