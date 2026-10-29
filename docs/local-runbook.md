# Local runbook and compatibility findings

Date: 29 October 2026. All records/users are synthetic. No cloud or paid-model calls.

## Prerequisites and setup

Windows PowerShell 7, Ubuntu WSL with a running Docker engine, and outbound access
to Microsoft's SDK metadata/downloads, NuGet and Playwright's Chromium distribution.
The application/client run on Windows; only Keycloak and PostgreSQL run in Docker.
Ports 5180-5183 must be available on loopback. Do not change the firewall/network or
stop somebody else's process to free them.

From the repository root:

```powershell
pwsh -File scripts/setup.ps1
pwsh -File scripts/dependencies.ps1 up
```

Wait until Keycloak's discovery endpoint is ready:
`http://127.0.0.1:5182/realms/delivery-demo/.well-known/openid-configuration`.
Then run:

```powershell
& ./.local/dotnet/dotnet.exe test tests/DeliveryExceptions.Tests --no-restore --disable-build-servers -m:1 --filter Category=Compatibility
pwsh -File scripts/stop.ps1
```

Setup keeps a checksum-verified SDK and browser under ignored `.local/`. Test/browser
packages are locked. No TLS verification is disabled; the browser installer uses
Node's existing system trust store through a child-process-scoped option.

Private credentials and generated realm import are under `.local/`. Never publish
them or raw provider/browser traces. Setup reuses those credentials; if private
credentials are missing while data volumes exist, it refuses to silently replace them.
Do not delete evidence volumes. The stop command preserves their data.

## Observed Task 1 result

Pinned SDK 10.0.403/runtime 10.0.14, MCP SDK 2.3.0, protocol 2026-07-28,
Keycloak 26.8.1, PostgreSQL 17.11, Playwright 1.64.1 with its matching Chromium.
Image digests are recorded in `infra/versions.json`; runtime versions were read from
the running local binaries/containers.

Ten compatibility tests passed, zero failed/skipped. They cover the exact challenge
and resource metadata, genuine browser PKCE login and empty SDK tool discovery,
unknown and changed resource rejection, wrong verifier, mismatched state, wrong
callback path, occupied callback listener, unexpected host/origin and anonymous
registration denial. The changed-resource/wrong-verifier test has two data rows.

For unknown resources, Keycloak permits the browser authorization stage but refuses
the subsequent exchange with `invalid_target`; no access token is issued. The report
does not claim earlier rejection. Changed target and incorrect PKCE verifier also
produce provider errors without successful tokens. State mismatch is rejected by the
SDK before exchange. No password or client-credentials grant acquires user tokens.

Initial failures are retained privately: the scaffold allowed anonymous HTTP 200;
the first client import omitted Keycloak's standard `basic` subject mapper; and the
empty server initially had no `tools/list` handler. The final import includes `basic`
and the service still rejects subjectless tokens. No validation was relaxed.

The first buffered-body readiness probe timed out locally; probes now inspect response
headers immediately. Builds use one worker and disabled build servers after an initial
multi-process stall. Those setup findings are not counted as successful security tests.

A subsequent repeat hit the original 60-second test budget before credentials were
read, after slow browser startup/navigation. That failed repeat remains retained.
The client now allows 150 seconds for initialization, with the unchanged 120-second
callback timeout and a 180-second test budget. A fresh native repeat passed all ten
tests in 40 seconds. Token lifetime, issuer/audience checks and PKCE remain unchanged.

Only the compatibility gate is accepted at this point. Tenant reads/writes, durable
audit, full token-negative acceptance, restart evidence and the final report remain.
