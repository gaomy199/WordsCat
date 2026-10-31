param([string]$PublishPath = (Join-Path $PSScriptRoot 'publish'), [switch]$IncludeUi)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'scripts/check-secrets.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Secret scan failed; only locations were reported.' }
$exe = Join-Path $PublishPath 'WordsCat.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Run build.ps1 before verify.ps1.' }
$taskResultDirectory = Join-Path $PSScriptRoot ('.test-results\' + [Guid]::NewGuid().ToString('N'))
$fixtureDirectory = Join-Path $PSScriptRoot 'fixtures'
$testArguments = '--self-test --data-dir "' + $taskResultDirectory + '" --regression-dir "' + $fixtureDirectory + '"'
$taskProcess = Start-Process -FilePath $exe -ArgumentList $testArguments -WindowStyle Hidden -PassThru -Wait
if ($taskProcess.ExitCode -ne 0) {
    $failure = Join-Path $taskResultDirectory 'test-failure.txt'
    if (Test-Path -LiteralPath $failure) { Get-Content -LiteralPath $failure }
    throw 'ReadDock self-test failed.'
}
Get-Content -LiteralPath (Join-Path $taskResultDirectory 'test-results.txt')
Write-Host "Report: $taskResultDirectory"
if ($IncludeUi) {
    $taskUiArguments = '--render-self-test --data-dir "' + $taskResultDirectory + '\ui"'
    $taskUiProcess = Start-Process -FilePath $exe -ArgumentList $taskUiArguments -WindowStyle Normal -PassThru -Wait
    if ($taskUiProcess.ExitCode -ne 0) {
        Get-Content -LiteralPath (Join-Path $taskResultDirectory 'ui\ui-test-failure.txt') -ErrorAction SilentlyContinue
        throw 'ReadDock real UI/render self-test failed.'
    }
    Get-Content -LiteralPath (Join-Path $taskResultDirectory 'ui\ui-test-results.txt')
}
