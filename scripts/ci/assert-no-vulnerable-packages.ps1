<#
.SYNOPSIS
  Gives `dotnet list package --vulnerable` a real exit code (the command itself always exits 0).
  Lists every vulnerable package; fails only on High or Critical advisories.

.EXAMPLE
  dotnet list KitchenAssistant.slnx package --vulnerable --include-transitive --format json > vulnerable.json
  ./scripts/ci/assert-no-vulnerable-packages.ps1 -Path vulnerable.json
#>
param(
  [Parameter(Mandatory = $true)]
  [string]$Path
)

$ErrorActionPreference = 'Stop'

$report = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
$blocking = 0
$found = 0

foreach ($project in @($report.projects)) {
  foreach ($framework in @($project.frameworks)) {
    $packages = @($framework.topLevelPackages) + @($framework.transitivePackages)
    foreach ($package in $packages) {
      if ($null -eq $package) { continue }
      foreach ($vuln in @($package.vulnerabilities)) {
        if ($null -eq $vuln) { continue }
        $found++
        Write-Host "$($vuln.severity): $($package.id) $($package.resolvedVersion) - $($vuln.advisoryurl) ($($project.path))"
        if ($vuln.severity -in @('High', 'Critical')) { $blocking++ }
      }
    }
  }
}

if ($blocking -gt 0) {
  Write-Host "$blocking High/Critical advisory finding(s)."
  exit 1
}

Write-Host "No High/Critical advisories ($found lower-severity finding(s))."
exit 0
