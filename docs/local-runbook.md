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
& ./.local/dotnet/dotnet.exe test tests/DeliveryExceptions.Tests --configuration Release --no-restore --disable-build-servers -m:1 --filter Category=Compatibility
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

## Standalone CLI demonstration

The following is the actual command-line client, with browser-based user sign-in.
Initialize local data privately, then start the service in a separate terminal:

```powershell
pwsh -File scripts/service.ps1 -Initialize
pwsh -File scripts/service.ps1
```

In another terminal:

```powershell
& ./.local/dotnet/dotnet.exe src/DeliveryExceptions.Client/bin/Release/net10.0/DeliveryExceptions.Client.dll list
& ./.local/dotnet/dotnet.exe src/DeliveryExceptions.Client/bin/Release/net10.0/DeliveryExceptions.Client.dll get --id a0000000-0000-0000-0000-000000000001
& ./.local/dotnet/dotnet.exe src/DeliveryExceptions.Client/bin/Release/net10.0/DeliveryExceptions.Client.dll review --id a0000000-0000-0000-0000-000000000001 --expected-version 1
```

Use only the generated synthetic users in `.local/credentials.json` at the local
Keycloak page. Do not save their passwords in the browser. To change users, sign out
of this local realm or use a separate browser profile; actual token identity decides
the tenant and permissions. Nothing signs into a cloud or employer account.

The review command is an explicit decision with the version read beforehand. A review
succeeds once; subsequent or stale attempts conflict. Initialization is idempotent and
does not undo reviews. The acceptance harness privately resets independent test cases.
Stop the foreground service with Ctrl+C before stopping dependencies.

On 29 October, the real Release CLI and normal Chrome UI completed a read and review
(exit 0/0), retained reviewed version 2 after an actual service restart (exit 0), and
refused the stale version-1 review (exit 1). The success and conflict audit records were
read back from PostgreSQL. Malformed CLI usage returned exit 2 without sign-in.
The manual source was a recorded working-tree digest based on `ce4b332`; it is not
represented as a clean exact-commit measurement. Private transcripts and a token-free
callback screenshot are retained under `.local/`.

During Task 4 self-review, the original unknown-resource fixture was found to also
change targets between authorization and exchange. The test now sends the same
unregistered URI in both requests and independently verifies `invalid_target` and
zero successful token responses. The separate changed-resource case remains.

The Audit/Client tests also prove durable denial/conflict events, no unverified identity
in invalid-token audit, read/write refusal when audit storage is unavailable, actual
HTTP 200 tool errors versus HTTP 401 transport errors, and callback-listener cleanup.
Timeout cleanup is exercised using a 300ms injected timeout; the normal callback
default remains 120 seconds. No claim of an observed two-minute wait follows.

## Full local evidence run

After setup, start dependencies and wait for the issuer discovery URL to return 200.
All commands below run from the product repository using PowerShell 7. Commit local
source changes first: check/acceptance refuse a dirty or mismatched source tree.

```powershell
./scripts/setup.ps1
./scripts/dependencies.ps1 up
./scripts/check.ps1
./scripts/acceptance.ps1
```

Checks perform locked restore, format verification, Release build and every focused
test. Acceptance repeats twelve named groups, including browser-issued ten-second
expiry and a fresh sign-in; raw expired-token probes bypass SDK refresh. Normal
client tokens last 120 seconds. The same live ID token is rejected as an API token.
Signed issuer/audience/expiry fixtures use the production validation parameters with
a test-only key trusted only by an isolated validator. They are also sent to the live
service, which rejects that untrusted key; that HTTP result alone does not isolate
issuer/audience validation. No fixture signing key is added to the application.

Each timestamped ignored result directory retains success/failure/not_run and private
TRX/logs. Only allowlisted aggregate JSON/transcript/cleanup data is suitable for
publication. Latest aggregate copies do not erase timestamped attempts. Database
fixtures reset privately between cases. Normal application startup never resets them.
The final acceptance script stops only this Compose project, checks demo listeners,
and retains both named evidence volumes. Fixture disposal owns its exact child process
handles and Playwright browser contexts. Run stop.ps1 if a session is interrupted.
It deliberately does not terminate a manually launched foreground service: stop that
terminal with Ctrl+C first.

The frozen fixture manifest is checked against actual bytes. Source commit/tree and
configuration hashes are recorded both before and after checks/acceptance. A later
documentation commit can describe an earlier measured source; its SHA must not be
presented as the measured commit. A failing prerequisite is a failed attempt, never a
passing skip.

On a fresh clone the current realm template imports automatically. Updating an
existing synthetic realm requires an explicit offline import while only its Keycloak
container is stopped, using the same private credential file and named volume. Do
not delete evidence volumes to conceal earlier attempts. The dedicated test client
uses the standard client Access Token Lifespan override of 10 seconds, as described in
[Keycloak advanced OIDC settings](https://www.keycloak.org/docs/26.7.0/server_admin/index.html).
The reviewed CI YAML shows native Docker on Ubuntu and no cloud credentials. It has
not been run on hosted CI. Windows/WSL execution is the measured environment.
