param()
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'check-secrets.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Secret scan failed; only locations were reported.' }
Push-Location -LiteralPath $taskRoot
try {
    $taskPaths = @(git -c core.quotepath=false ls-files --cached --others --exclude-standard | Sort-Object -Unique | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate repository files.' }
    $taskProblems = [Collections.Generic.List[string]]::new()
    $taskSecretPattern = '(?i)(github_pat_[A-Za-z0-9_]{20,}|gh[pousr]_[A-Za-z0-9]{20,}|sk-[A-Za-z0-9]{24,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)'
    $taskBinaryExtensions = @('.ico', '.png', '.jpg', '.jpeg', '.gif', '.woff', '.woff2', '.ttf')
    foreach ($taskPath in $taskPaths) {
        if ($taskPath -match '(^|/)(Data|PreviewData|publish|dist|bin|obj|\.build|\.test-results|\.vs|\.git)(/|$)|(^|/)fixtures/private(/|$)|(^|/)(settings\.json|\.env(?:\..*)?)$|\.(pfx|pem|key|p12|p8|zip|log|bak|tmp)$') {
            $taskProblems.Add('Generated, private, or credential file: ' + $taskPath)
        }
        if ($taskPath -match '[^\x20-\x7e]') { $taskProblems.Add('Non-English repository path: ' + $taskPath) }
        if ([IO.Path]::GetExtension($taskPath).ToLowerInvariant() -notin $taskBinaryExtensions) {
            $taskContent = [IO.File]::ReadAllText((Join-Path $taskRoot $taskPath))
            if ($taskContent -match $taskSecretPattern) { $taskProblems.Add('Potential credential found (value hidden): ' + $taskPath) }
        }
    }
    if ($taskProblems.Count -gt 0) { throw ($taskProblems -join [Environment]::NewLine) }
    Write-Output ('PASS: ' + $taskPaths.Count + ' source files checked; English paths, no generated/private data, no detected credential patterns.')
    Write-Output 'This check does not read Windows Credential Manager or contact a network service.'
}
finally { Pop-Location }
