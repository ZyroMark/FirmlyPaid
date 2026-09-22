<#
.SYNOPSIS
    Asks every FirmlyPaid service for its health, from the host.
.DESCRIPTION
    The step 1 checkpoint: after "docker compose up -d --build", every service should
    answer /health with status Healthy and its own name.
#>
[CmdletBinding()]
param(
    [int]$TimeoutSeconds = 5
)

$services = [ordered]@{
    'Gateway'             = 5100
    'Enrolment.Api'       = 5101
    'Matching.Api'        = 5102
    'AccountLink.Api'     = 5103
    'Payments.Api'        = 5104
    'Risk.Api'            = 5105
    'TillIntegration.Api' = 5106
}

$allHealthy = $true

foreach ($name in $services.Keys) {
    $port = $services[$name]
    $url = "http://localhost:$port/health"

    try {
        $response = Invoke-RestMethod -Uri $url -TimeoutSec $TimeoutSeconds -ErrorAction Stop
        if ($response.status -eq 'Healthy') {
            Write-Host ("  OK       {0,-22} {1}  ({2})" -f $name, $url, $response.service) -ForegroundColor Green
        }
        else {
            Write-Host ("  DEGRADED {0,-22} {1}  ({2})" -f $name, $url, $response.status) -ForegroundColor Yellow
            $allHealthy = $false
        }
    }
    catch {
        Write-Host ("  DOWN     {0,-22} {1}" -f $name, $url) -ForegroundColor Red
        $allHealthy = $false
    }
}

if ($allHealthy) {
    Write-Host "`nAll FirmlyPaid services are healthy." -ForegroundColor Green
    exit 0
}

Write-Host "`nOne or more services are not healthy. Try: docker compose logs -f" -ForegroundColor Red
exit 1
