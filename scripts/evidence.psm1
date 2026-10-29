function New-AcceptanceReport($Identity, $Cases, $Cleanup, [int]$TestExitCode=0) {
  $keys=@('source','tree','fixture','config')
  $safeIdentity=[ordered]@{}
  foreach($key in $keys) {
    $size=if($key -in @('source','tree')){40}else{64}
    if($Identity[$key] -cnotmatch "^[0-9a-f]{$size}$"){throw "Invalid identity: $key"}
    $safeIdentity[$key]=$Identity[$key]
  }
  if(@($Cases).Count -ne 12){throw 'Exactly twelve cases required'}
  $safeCases=@(); $seen=@{}
  foreach($case in $Cases) {
    if($case.id -cnotmatch '^A([1-9]|1[0-2])$' -or $seen.ContainsKey($case.id)){throw 'Missing or duplicate case ID'}
    $seen[$case.id]=$true
    foreach($key in $keys){if($case.identity[$key] -cne $Identity[$key]){throw 'Case identity mismatch'}}
    if($case.status -cnotin @('pass','fail','not_run')){throw 'Unknown status'}
    if($case.provenance -cnotin @('live-local','live-local-and-labelled-fixtures')){throw 'Unsupported provenance'}
    $safeCases+=@{id=$case.id;status=$case.status;identity=$safeIdentity;provenance=$case.provenance}
  }
  $passed=($Cleanup.passed -is [bool]) -and $Cleanup.passed
  return [ordered]@{schemaVersion=1;measuredAt=[DateTimeOffset]::UtcNow.ToString('o');identity=$safeIdentity;cases=$safeCases;cleanup=@{passed=$passed};testExitCode=$TestExitCode;accepted=($TestExitCode -eq 0 -and $passed -and @($safeCases|Where-Object status -ne 'pass').Count -eq 0);environment='local-loopback';modelCalls=0;hostedCiProven=$false;remoteHttpsProven=$false}
}
function Invoke-EvidenceRun($Body, $Cleanup) { try { & $Body } finally { & $Cleanup } }
Export-ModuleMember -Function New-AcceptanceReport,Invoke-EvidenceRun
function Get-EvidenceIdentity {
  $source=(& git rev-parse HEAD).Trim(); if($LASTEXITCODE -ne 0){throw 'Source missing'}
  $tree=(& git rev-parse 'HEAD^{tree}').Trim()
  $dirty=@(& git status --porcelain --untracked-files=normal)
  if($dirty.Count -gt 0){throw 'Commit reviewed local source before an acceptance measurement'}
  $manifest=Get-Content fixtures/manifest.json -Raw | ConvertFrom-Json
  foreach($entry in $manifest.files){if((Get-FileHash (Join-Path fixtures $entry.path) -Algorithm SHA256).Hash.ToLowerInvariant() -cne $entry.sha256){throw 'Fixture digest mismatch'}}
  $hashes=@('global.json','Directory.Packages.props','infra/compose.yaml','infra/versions.json','infra/keycloak/realm-template.json')|ForEach-Object {"$_`:$((Get-FileHash $_ -Algorithm SHA256).Hash.ToLowerInvariant())"}
  $digest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($hashes -join "`n")))).ToLowerInvariant()
  return @{source=$source;tree=$tree;fixture=(Get-FileHash fixtures/manifest.json -Algorithm SHA256).Hash.ToLowerInvariant();config=$digest}
}
function Read-AcceptanceCases($Path,$Identity) {
  $cases=@(1..12|ForEach-Object { @{id="A$_";status='not_run';identity=$Identity;provenance=$(if($_ -eq 7){'live-local-and-labelled-fixtures'}else{'live-local'})} })
  if(!(Test-Path -LiteralPath $Path)){return $cases}
  [xml]$trx=Get-Content -LiteralPath $Path -Raw
  $seen=@{}
  foreach($result in $trx.TestRun.Results.UnitTestResult){
    if($result.testName -notmatch '\.A(\d{2})_'){throw 'Unexpected acceptance test result'}
    $number=[int]$Matches[1]
    if($number -lt 1 -or $number -gt 12 -or $seen.ContainsKey($number)){throw 'Duplicate/unrecognized acceptance result'}
    $seen[$number]=$true
    $cases[$number-1].status=switch($result.outcome){'Passed'{'pass'} 'NotExecuted'{'not_run'} default{'fail'}}
  }
  return $cases
}
Export-ModuleMember -Function Get-EvidenceIdentity,Read-AcceptanceCases
