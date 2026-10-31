param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'publish'),
    [switch]$SelfContained,
    [switch]$Offline,
    [ValidateSet('win-x64')][string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$taskBuildRoot = Join-Path $PSScriptRoot '.build'
$env:DOTNET_CLI_HOME = $taskBuildRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$project = Join-Path $PSScriptRoot 'src\WordsCat\WordsCat.csproj'
$taskPublishArgs = @('publish', $project, '-c', 'Release', '--self-contained', $SelfContained.IsPresent.ToString().ToLowerInvariant(), '-p:PlatformTarget=x64', '-p:DebugType=None', '-p:NuGetAudit=false', '-o', $OutputPath)
if ($SelfContained) { $taskPublishArgs += @('-r', $Runtime, '-p:PublishTrimmed=false') }
if ($Offline) { $taskPublishArgs += '--no-restore' }
dotnet @taskPublishArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host "Built: $OutputPath"
