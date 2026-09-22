<#
.SYNOPSIS
    Brings the whole FirmlyPaid stack up locally and reports health.
#>
[CmdletBinding()]
param(
    [switch]$Rebuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Test-Path (Join-Path $root '.env'))) {
    Write-Host 'No .env found. Copying .env.example - edit it and set your own passwords.' -ForegroundColor Yellow
    Copy-Item (Join-Path $root '.env.example') (Join-Path $root '.env')
}

$composeArgs = @('compose', 'up', '-d')
if ($Rebuild) { $composeArgs += '--build' }

docker @composeArgs
if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed.' }

Write-Host "`nWaiting for services to answer /health ..." -ForegroundColor Cyan
for ($attempt = 1; $attempt -le 30; $attempt++) {
    Start-Sleep -Seconds 2
    & (Join-Path $PSScriptRoot 'check-health.ps1') | Out-Null
    if ($LASTEXITCODE -eq 0) { break }
}

& (Join-Path $PSScriptRoot 'check-health.ps1')
