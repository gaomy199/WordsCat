param(
    [ValidateSet('win-x64')][string]$Runtime = 'win-x64',
    [string]$OutputDirectory = '',
    [switch]$Force,
    [switch]$Offline,
    [switch]$RequireClean
)
$ErrorActionPreference = 'Stop'
# Local packaging only. GitHub upload is performed by the tag workflow.
& (Join-Path $PSScriptRoot 'package-release.ps1') @PSBoundParameters
