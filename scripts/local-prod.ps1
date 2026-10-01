<#
.SYNOPSIS
  Build and run the production-like local stack (compose.yaml): the app as a Linux container
  in Production mode against a SQL Server container, at http://localhost:8090.

.EXAMPLE
  ./scripts/local-prod.ps1          # build image, start stack, wait for /healthz
  ./scripts/local-prod.ps1 -NoBuild # restart with the existing image
  ./scripts/local-prod.ps1 -Reset   # also wipe the SQL volume (schema is rebuilt by startup migrations)
  ./scripts/local-prod.ps1 -Down    # stop the stack, keep data
#>
param(
    [switch]$Down,
    [switch]$Reset,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

function Invoke-Checked([scriptblock]$Command, [string]$What) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$What failed (exit $LASTEXITCODE)." }
}

function New-SaPassword {
    # Meets the SQL Server policy; avoids characters that break .env or connection strings ($ ; " ' = #).
    $sets = 'ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnopqrstuvwxyz', '23456789', '!%*-_+.'
    $all = -join $sets
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 24
    $rng.GetBytes($bytes)
    $chars = for ($i = 0; $i -lt 20; $i++) { $all[$bytes[$i] % $all.Length] }
    # Guarantee one character from each class.
    $chars += for ($i = 0; $i -lt 4; $i++) { $sets[$i][$bytes[20 + $i] % $sets[$i].Length] }
    -join $chars
}

function Initialize-EnvFile {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    if (-not (Test-Path .env)) {
        Copy-Item .env.example .env
        Write-Host 'Created .env from .env.example (git-ignored).'
    }
    $lines = [System.IO.File]::ReadAllLines((Resolve-Path .env))
    $changed = $false
    $lines = foreach ($line in $lines) {
        if ($line -match '^MSSQL_SA_PASSWORD=\s*$') {
            $changed = $true
            "MSSQL_SA_PASSWORD=$(New-SaPassword)"
        } else { $line }
    }
    if ($changed) {
        [System.IO.File]::WriteAllLines((Resolve-Path .env), $lines, $utf8NoBom)
        Write-Host 'Generated a random MSSQL_SA_PASSWORD in .env (not printed).'
    }
}

if ($Down) {
    Invoke-Checked { docker compose down } 'docker compose down'
    return
}

Initialize-EnvFile

if ($Reset) {
    Invoke-Checked { docker compose down -v } 'docker compose down -v'
}

if (-not $NoBuild) {
    Write-Host 'Building container image kitchen-assistant:local ...'
    Invoke-Checked {
        dotnet publish KitchenAssistant.csproj -c Release --os linux --arch x64 /t:PublishContainer -p:ContainerImageTag=local --nologo -v:minimal
    } 'dotnet publish /t:PublishContainer'
}

Write-Host 'Starting SQL Server and waiting until it is healthy ...'
Invoke-Checked { docker compose up -d --wait sql } 'docker compose up sql'

Write-Host 'Starting the app ...'
Invoke-Checked { docker compose up -d --force-recreate app } 'docker compose up app'

$url = 'http://localhost:8090'
$deadline = (Get-Date).AddSeconds(120)
while ((Get-Date) -lt $deadline) {
    try {
        $response = Invoke-WebRequest "$url/healthz" -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            Write-Host "Ready: $url  (health: $($response.Content))"
            return
        }
    } catch { Start-Sleep -Seconds 3 }
}

docker compose logs --tail 60 app
throw "App did not become healthy at $url/healthz within 120 s. See the logs above."
