<#
.SYNOPSIS
    Saves each service's OpenAPI document into docs\api.
.DESCRIPTION
    Run this with the stack up. The files in docs\api are a deliverable, so refresh them
    whenever endpoints change.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $root 'docs\api'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$services = [ordered]@{
    'FirmlyPaid.Gateway'             = 5100
    'FirmlyPaid.Enrolment.Api'       = 5101
    'FirmlyPaid.Matching.Api'        = 5102
    'FirmlyPaid.AccountLink.Api'     = 5103
    'FirmlyPaid.Payments.Api'        = 5104
    'FirmlyPaid.Risk.Api'            = 5105
    'FirmlyPaid.TillIntegration.Api' = 5106
}

foreach ($name in $services.Keys) {
    $port = $services[$name]
    $target = Join-Path $outputDirectory "$name.openapi.json"

    try {
        Invoke-WebRequest -Uri "http://localhost:$port/swagger/v1/swagger.json" -OutFile $target -TimeoutSec 10
        Write-Host ("  saved {0}" -f $target) -ForegroundColor Green
    }
    catch {
        Write-Host ("  could not reach {0} on port {1}. Is the stack up?" -f $name, $port) -ForegroundColor Red
    }
}
