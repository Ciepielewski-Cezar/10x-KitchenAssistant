<#
.SYNOPSIS
  Fails unless at least one test actually executed. `dotnet test` exits 0 when a filter matches
  nothing or a test project is missing from the solution, so a green run alone proves nothing.

.EXAMPLE
  ./scripts/ci/assert-tests-ran.ps1 -ResultsDirectory TestResults
#>
param(
  [Parameter(Mandatory = $true)]
  [string]$ResultsDirectory
)

$ErrorActionPreference = 'Stop'

$trx = @()
if (Test-Path -LiteralPath $ResultsDirectory) {
  $trx = @(Get-ChildItem -LiteralPath $ResultsDirectory -Filter '*.trx' -Recurse -File)
}

if ($trx.Count -eq 0) {
  Write-Host "No TRX files found in '$ResultsDirectory': the test step produced no results."
  exit 1
}

$executed = 0
foreach ($file in $trx) {
  [xml]$doc = Get-Content -LiteralPath $file.FullName -Raw
  $counters = $doc.TestRun.ResultSummary.Counters
  if ($null -ne $counters) {
    $executed += [int]$counters.executed
  }
}

if ($executed -le 0) {
  Write-Host "0 tests executed across $($trx.Count) TRX file(s) in '$ResultsDirectory'."
  exit 1
}

Write-Host "Executed tests: $executed"
exit 0
