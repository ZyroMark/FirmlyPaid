<#
.SYNOPSIS
    Stops the FirmlyPaid stack. Add -Clean to also delete the database and queue volumes.
#>
[CmdletBinding()]
param(
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

if ($Clean) {
    docker compose down -v
    Write-Host 'Stack stopped and local data volumes removed.' -ForegroundColor Yellow
}
else {
    docker compose down
    Write-Host 'Stack stopped. Data volumes kept.' -ForegroundColor Green
}
