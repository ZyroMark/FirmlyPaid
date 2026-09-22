<#
.SYNOPSIS
    Creates both FirmlyPaid databases and fills them with the demo data.
.DESCRIPTION
    Reads your .env file, points the tool at the SQL Server running in Docker, then runs
    migrate and seed. Safe to run again: seeding skips if the demo data is already there.
.PARAMETER Command
    migrate, seed, reset, status or verify-audit. Defaults to running migrate then seed.
.EXAMPLE
    .\scripts\db-setup.ps1
.EXAMPLE
    .\scripts\db-setup.ps1 -Command status
#>
[CmdletBinding()]
param(
    [ValidateSet('all', 'migrate', 'seed', 'reset', 'status', 'verify-audit')]
    [string]$Command = 'all',

    [string]$SqlServer = 'localhost,14330'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$envFile = Join-Path $root '.env'
if (-not (Test-Path $envFile)) {
    throw "No .env file found. Copy .env.example to .env and set your passwords first."
}

# Read .env into a lookup. Lines that are blank or commented are ignored.
$settings = @{}
foreach ($line in Get-Content $envFile) {
    $trimmed = $line.Trim()
    if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }

    $separator = $trimmed.IndexOf('=')
    if ($separator -lt 1) { continue }

    $settings[$trimmed.Substring(0, $separator).Trim()] = $trimmed.Substring($separator + 1).Trim()
}

foreach ($required in @('MSSQL_SA_PASSWORD', 'FIRMLYPAID_ID_PEPPER')) {
    if (-not $settings.ContainsKey($required) -or [string]::IsNullOrWhiteSpace($settings[$required])) {
        throw "Your .env is missing $required. See .env.example."
    }
}

$password = $settings['MSSQL_SA_PASSWORD']

# These live for this process only, so the pepper never reaches your shell history.
$env:ConnectionStrings__Core = "Server=$SqlServer;Database=FirmlyPaidCore;User Id=sa;Password=$password;TrustServerCertificate=True;Encrypt=False"
$env:ConnectionStrings__Vault = "Server=$SqlServer;Database=FirmlyPaidVault;User Id=sa;Password=$password;TrustServerCertificate=True;Encrypt=False"
$env:FIRMLYPAID_ID_PEPPER = $settings['FIRMLYPAID_ID_PEPPER']

$commands = if ($Command -eq 'all') { @('migrate', 'seed') } else { @($Command) }

foreach ($step in $commands) {
    Write-Host "`n=== dbtool $step ===" -ForegroundColor Cyan
    dotnet run --project src\FirmlyPaid.DbTool --no-launch-profile -- $step
    if ($LASTEXITCODE -ne 0) { throw "dbtool $step failed with exit code $LASTEXITCODE." }
}
