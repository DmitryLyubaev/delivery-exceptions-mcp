$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
Push-Location $projectRoot
try {
  Import-Module ./scripts/evidence.psm1 -Force
  $identity=Get-EvidenceIdentity
  New-Item -ItemType Directory .local/results -Force|Out-Null
  @{identity=$identity;passed=$false;measuredAt=[DateTimeOffset]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 6|Set-Content .local/results/check.json
  $dotnet=if($IsWindows){Join-Path $projectRoot '.local/dotnet/dotnet.exe'}else{'dotnet'}
  & $dotnet restore --locked-mode --disable-build-servers -m:1
  if($LASTEXITCODE -ne 0){throw 'Locked restore failed'}
  & $dotnet format DeliveryExceptions.sln --verify-no-changes --no-restore
  if($LASTEXITCODE -ne 0){throw 'Formatting check failed'}
  & $dotnet build -c Release --no-restore --disable-build-servers -m:1
  if($LASTEXITCODE -ne 0){throw 'Release build failed'}
  & $dotnet test tests/DeliveryExceptions.Tests -c Release --no-build --no-restore --filter 'Category!=Acceptance' --logger 'trx;LogFileName=checks.trx' --results-directory .local/results
  if($LASTEXITCODE -ne 0){throw 'Checks failed'}
  $after=Get-EvidenceIdentity
  foreach($key in $identity.Keys){if($after[$key] -cne $identity[$key]){throw 'Source changed during checks'}}
  [xml]$trx=Get-Content .local/results/checks.trx -Raw
  if([int]$trx.TestRun.ResultSummary.Counters.total -eq 0 -or [int]$trx.TestRun.ResultSummary.Counters.total -ne [int]$trx.TestRun.ResultSummary.Counters.passed){throw 'Incomplete check results'}
  @{identity=$identity;passed=$true;total=[int]$trx.TestRun.ResultSummary.Counters.total;passedTests=[int]$trx.TestRun.ResultSummary.Counters.passed;measuredAt=[DateTimeOffset]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 6|Set-Content .local/results/check.json
} finally {Pop-Location}
