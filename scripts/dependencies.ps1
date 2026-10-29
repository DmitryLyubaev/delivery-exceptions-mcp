param([ValidateSet('up','stop','status')][string]$Action = 'status')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
$linuxRoot = (& wsl -d Ubuntu -- wslpath -a $projectRoot.Replace('\','/')).Trim()
$composeArgs = @('-d','Ubuntu','--','docker','compose','--project-name','p18-delivery-exceptions','--env-file',"$linuxRoot/.local/docker.env",'-f',"$linuxRoot/infra/compose.yaml")
switch ($Action) {
  'up' { & wsl @composeArgs up -d }
  'stop' { & wsl @composeArgs stop }
  'status' { & wsl @composeArgs ps }
}
if ($LASTEXITCODE -ne 0) { throw "Project dependency action failed: $Action" }
