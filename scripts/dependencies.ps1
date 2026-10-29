param([ValidateSet('up','stop','status')][string]$Action = 'status')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
if ($IsWindows) {
  $linuxRoot = (& wsl -d Ubuntu -- wslpath -a $projectRoot.Replace('\','/')).Trim()
  $engine='wsl'; $composeArgs=@('-d','Ubuntu','--','docker')
} else { $linuxRoot=$projectRoot; $engine='docker'; $composeArgs=@() }
$composeArgs+=@('compose','--project-name','p18-delivery-exceptions','--env-file',"$linuxRoot/.local/docker.env",'-f',"$linuxRoot/infra/compose.yaml")
switch ($Action) {
  'up' { & $engine @composeArgs up -d }
  'stop' { & $engine @composeArgs stop }
  'status' { & $engine @composeArgs ps }
}
if ($LASTEXITCODE -ne 0) { throw "Project dependency action failed: $Action" }
