$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
Push-Location $projectRoot
try {
  Import-Module ./scripts/evidence.psm1 -Force
  $identity=Get-EvidenceIdentity
  $checks=Get-Content .local/results/check.json -Raw|ConvertFrom-Json -AsHashtable
  if(!$checks.passed){throw 'Run check.ps1 first'}
  foreach($key in $identity.Keys){if($checks.identity[$key] -cne $identity[$key]){throw 'Check and acceptance source differ'}}
  $run=Join-Path '.local/results' ([DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
  New-Item -ItemType Directory $run|Out-Null
  $script:testExit=1; $script:cleanup=@{passed=$false}
  try {
    Invoke-EvidenceRun {
      $dotnet=if($IsWindows){Join-Path $projectRoot '.local/dotnet/dotnet.exe'}else{'dotnet'}
      & $dotnet test tests/DeliveryExceptions.Tests -c Release --no-build --no-restore --filter Category=Acceptance --logger 'trx;LogFileName=acceptance.trx' --results-directory $run *> (Join-Path $run 'private-test.log')
      $script:testExit=$LASTEXITCODE
    } {
      try {
        & ./scripts/stop.ps1 *> (Join-Path $run 'private-stop.log')
        $engine=if($IsWindows){'wsl'}else{'docker'}; $engineArgs=if($IsWindows){@('-d','Ubuntu','--','docker')}else{@()}
        $running=@(& $engine @engineArgs ps --filter 'label=com.docker.compose.project=p18-delivery-exceptions' --format '{{.ID}}')
        if($LASTEXITCODE -ne 0){throw 'Container ownership check failed'}
        $ports=if($IsWindows){@(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue|Where-Object LocalPort -in @(5180,5181,5182,5183))}else{@(& ss -H -ltn | Where-Object {$_ -match ':(5180|5181|5182|5183)\s'})}
        $volumes=@(& $engine @engineArgs volume ls --filter 'label=com.docker.compose.project=p18-delivery-exceptions' --format '{{.Name}}')
        $script:cleanup=@{passed=($running.Count -eq 0 -and $ports.Count -eq 0 -and $volumes.Count -ge 2);ownedContainersRunning=$running.Count;demoListeners=$ports.Count;retainedVolumes=$volumes.Count;browserOwnership='Playwright contexts disposed by awaited fixture';unownedResources='separately tested by ReportLifecycleTests'}
      } catch {$script:cleanup=@{passed=$false}}
    }
  } finally {
    $cases=(Read-AcceptanceCases (Join-Path $run 'missing.trx') $identity).cases; $resultValidationPassed=$false
    try {$parsed=Read-AcceptanceCases (Join-Path $run 'acceptance.trx') $identity; $cases=$parsed.cases; $resultValidationPassed=$parsed.valid; if(!$parsed.valid){$script:testExit=1}} catch {$script:testExit=1}
    try {$after=Get-EvidenceIdentity; foreach($key in $identity.Keys){if($after[$key] -cne $identity[$key]){$script:testExit=1}}} catch {$script:testExit=1}
    $report=New-AcceptanceReport $identity $cases $script:cleanup $script:testExit $resultValidationPassed
    $report|ConvertTo-Json -Depth 12|Set-Content (Join-Path $run 'acceptance.json')
    $script:cleanup|ConvertTo-Json -Depth 5|Set-Content (Join-Path $run 'cleanup.json')
    @('Local deterministic MCP acceptance. Zero model calls. No remote HTTPS or hosted CI claim.',"source=$($identity.source)","tree=$($identity.tree)","fixture=$($identity.fixture)","config=$($identity.config)") + @($cases|ForEach-Object {"$($_.id) $($_.status) $($_.provenance)"})|Set-Content (Join-Path $run 'transcript.txt')
    foreach($name in @('acceptance.json','cleanup.json','transcript.txt')){Copy-Item -LiteralPath (Join-Path $run $name) -Destination (Join-Path '.local/results' $name)}
    Write-Output "Evidence retained: $run; accepted=$($report.accepted)"
  }
  if(!$report.accepted){throw 'Acceptance or owned cleanup failed; inspect retained private attempt'}
} finally {Pop-Location}
