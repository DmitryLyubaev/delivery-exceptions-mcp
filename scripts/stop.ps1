$ErrorActionPreference = 'Stop'
# Test fixtures dispose their exact child process handles. This command only stops
# the dedicated Compose project and preserves its named evidence volumes.
& (Join-Path $PSScriptRoot 'dependencies.ps1') stop
if ($LASTEXITCODE -ne 0) { throw 'Dependency shutdown failed' }
