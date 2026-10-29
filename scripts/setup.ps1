$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
Push-Location $projectRoot
try {
  New-Item -ItemType Directory -Path '.local/downloads','.local/results' -Force | Out-Null
  $dotnetExe = Join-Path $projectRoot '.local/dotnet/dotnet.exe'
  if (-not (Test-Path -LiteralPath $dotnetExe)) {
    $metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
    $sdk = $metadata.releases | ForEach-Object { $_.sdks } | Where-Object version -eq '10.0.403' | Select-Object -First 1
    $archive = $sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.url.EndsWith('.zip') } | Select-Object -First 1
    if (-not $archive) { throw 'Approved SDK archive missing from release metadata' }
    Invoke-WebRequest $archive.url -OutFile '.local/downloads/dotnet-sdk.zip'
    if ((Get-FileHash '.local/downloads/dotnet-sdk.zip' -Algorithm SHA512).Hash -ne $archive.hash) { throw 'SDK checksum mismatch' }
    Expand-Archive '.local/downloads/dotnet-sdk.zip' -DestinationPath '.local/dotnet'
  }
  if (-not (Test-Path -LiteralPath '.local/credentials.json')) {
    $ownedVolumes = & wsl -d Ubuntu -- docker volume ls --filter 'label=com.docker.compose.project=p18-delivery-exceptions' --format '{{.Name}}'
    if ($ownedVolumes) { throw 'Existing project data needs its original private credentials; refusing to replace them' }
    $privateConfig = @{users=@{};adminPassword=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24));postgresPassword=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))}
    $realm = Get-Content 'infra/keycloak/realm-template.json' -Raw | ConvertFrom-Json
    foreach ($user in $realm.users) {
      $password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(20))
      $privateConfig.users[$user.username] = $password
      $user | Add-Member -NotePropertyName credentials -NotePropertyValue @(@{type='password';value=$password;temporary=$false})
    }
    $realm | ConvertTo-Json -Depth 30 | Set-Content '.local/realm.json'
    $privateConfig | ConvertTo-Json -Depth 10 | Set-Content '.local/credentials.json'
  }
  $versions = Get-Content 'infra/versions.json' -Raw | ConvertFrom-Json
  $privateConfig = Get-Content '.local/credentials.json' -Raw | ConvertFrom-Json
  @("KEYCLOAK_IMAGE=$($versions.keycloak)","POSTGRES_IMAGE=$($versions.postgres)","KEYCLOAK_ADMIN_PASSWORD=$($privateConfig.adminPassword)","POSTGRES_PASSWORD=$($privateConfig.postgresPassword)") | Set-Content '.local/docker.env'
  & $dotnetExe restore --locked-mode --disable-build-servers -m:1
  if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed' }
  & $dotnetExe build --no-restore --disable-build-servers -m:1 --configuration Release
  if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
  $previousNodeOptions = $env:NODE_OPTIONS
  $previousBrowserPath = $env:PLAYWRIGHT_BROWSERS_PATH
  try {
    $env:NODE_OPTIONS = "$previousNodeOptions --use-system-ca".Trim()
    $env:PLAYWRIGHT_BROWSERS_PATH = Join-Path $projectRoot '.local/browsers'
    pwsh tests/DeliveryExceptions.Tests/bin/Release/net10.0/playwright.ps1 install chromium
    if ($LASTEXITCODE -ne 0) { throw 'Matching Chromium installation failed' }
  } finally { $env:NODE_OPTIONS = $previousNodeOptions; $env:PLAYWRIGHT_BROWSERS_PATH = $previousBrowserPath }
  Write-Output 'Local setup ready. Start the two dependencies with scripts/dependencies.ps1 up.'
} finally { Pop-Location }
