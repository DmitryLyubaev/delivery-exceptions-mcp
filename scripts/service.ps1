param([switch]$Initialize, [ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
$privateConfig = Get-Content (Join-Path $projectRoot '.local/credentials.json') -Raw | ConvertFrom-Json
$previousConnection = $env:DELIVERY_DB_CONNECTION
Push-Location $projectRoot
try {
  $env:DELIVERY_DB_CONNECTION = "Host=127.0.0.1;Port=5183;Database=delivery_demo;Username=delivery_demo;Password=$($privateConfig.postgresPassword);SSL Mode=Disable;Timeout=5;Command Timeout=10"
  $dotnetExe = Join-Path $projectRoot '.local/dotnet/dotnet.exe'
  $serviceDll = Join-Path $projectRoot "src/DeliveryExceptions.Service/bin/$Configuration/net10.0/DeliveryExceptions.Service.dll"
  if ($Initialize) { & $dotnetExe $serviceDll --initialize-demo }
  else { & $dotnetExe $serviceDll }
  if ($LASTEXITCODE -ne 0) { throw 'Local service command failed' }
} finally { $env:DELIVERY_DB_CONNECTION = $previousConnection; Pop-Location }
