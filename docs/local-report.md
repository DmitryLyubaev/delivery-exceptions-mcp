# Local acceptance report - 29 October 2026

> Subsequent publication and hosted Ubuntu verification on 30 October are recorded in the [publication report](publication-report-2026-10-30.md). This document retains the dated Windows measurement.

This is a bounded synthetic CV demonstration on Windows with Ubuntu WSL Docker.
One final acceptance run covers twelve deterministic groups over six records, two
business tenants and three users. These are functional checks, not statistical trials
or a security certification. There are zero language-model evaluation calls.

The measured source is recorded below; this report is a later documentation artifact.
Private raw logs/TRX and credentials remain ignored. Only the allowlisted aggregate
and a redacted audit example are included. Earlier failed attempts are retained
privately, including missing provider setup and an invalid raw MCP request. Neither
was counted as passing acceptance.

The standalone Release CLI was also exercised through its OS-opened Chrome browser:
get (exit 0), explicit review with expected version 1 (exit 0, version 2), restart/get
(exit 0, version 2 retained), stale review (exit 1, conflict), invalid usage (exit 2).
That manual run records an earlier HEAD plus dirty-source digest separately, so it is
not represented as the clean-commit acceptance sample.

## Runtime and configuration

- Project-local .NET SDK 10.0.403; ASP.NET Core/.NET runtime 10.0.14.
- Official MCP C# SDK 2.3.0; negotiated protocol 2026-07-28, stateless HTTP.
- Keycloak 26.8.1 with experimental resource-indicators; normal token 120 seconds,
  dedicated test client 10 seconds, validation clock skew 2 seconds.
- PostgreSQL 17.11 and Npgsql 10.0.3; both dependency image digests in infra/versions.json.
- Microsoft.Playwright 1.64.1, matching Chromium 156.0.8078.4 revision 1248.
- Windows host service/client, WSL Ubuntu Docker 29.7.2 and Compose 5.4.0.

A7 uses real browser-issued access/ID tokens, genuine expiry and fresh sign-in. The
crafted issuer/audience/expiry cases have a positive same-key control in an isolated
validator using the production parameters. The live service never trusts that fixture
key; its rejection of those HTTP requests alone does not isolate issuer/audience checks.
A9 deliberately invokes forbidden tools after reading hostile carrier text. It proves
a deterministic authorization boundary, with no language-model robustness claim.

## Redacted audit shape

A successful review stores a server correlation ID, UTC time, verified issuer/subject,
resolved tenant, operation and delivery ID, allow/success, and versions 1 to 2 in the
same commit as the updated row. Invalid-token denial stores null trusted identity and
tenant. Conflict denial survives domain rollback. No token, callback code, verifier,
password, carrier note, raw request or header belongs in an audit row.

## Limits

Local loopback HTTP/development identity only. No remotely hosted HTTPS service,
production identity administration, employer integration, hosted CI execution, paid
model evaluation, availability target, customer adoption or compliance claim. The
GitHub workflow is an unpublished reproduction definition. No cloud resource was
created and no repository was published.

## Measured outcome

Measured at 10/29/2026 20:37:49. Locked restore and formatting passed; Release build
had zero warnings/errors. Focused tests: **54/54**, zero skips (1 minute 58 seconds).
Acceptance: **12/12**, zero skips (1 minute 5 seconds), one final local run.

- Source: `5c4b5be555a4aee72965605c9ffecc12688bf37d`
- Git tree: `9d111a540a1ffb6496174066ebc9e9b801336266`
- Fixture manifest SHA-256: `ba441258628949d2e13bab1deaeb09b01cf95316a0465f96fe18818d63ec5c26`
- Configuration-set SHA-256: `53ac38f1c4b1b599f5e054fafe4b9648cabca5d58e2a2951f2a33810e9211605`

| Group | Result | Observed boundary |
|---|---|---|
| A1 | pass | Discovery, S256 browser sign-in, exact resource and protocol |
| A2 | pass | Own-tenant delayed records for all three users |
| A3 | pass | Foreign and missing read have same bounded shape |
| A4 | pass | Permitted review and matching atomic audit, version 1 to 2 |
| A5 | pass | Scope and fixed entitlement both required for write |
| A6 | pass | Foreign write denied, full domain row unchanged |
| A7 | pass | Live expired/ID tokens and labelled crafted negative tokens |
| A8 | pass | Conflict conditions and exactly one concurrent mutation |
| A9 | pass | Hostile note returned as data; forbidden calls remain denied |
| A10 | pass | Durable audit, outage fail-closed, no credential/token output |
| A11 | pass | Application restart retains reviewed row and audit; fresh sign-in |
| A12 | pass | Unknown/changed target, wrong verifier and mismatched state rejected |

Cleanup: **pass**, zero running owned containers, zero listeners on ports 5180-5183,
two retained named volumes. Independently owned canary process, listener and container
survived the stop command in the focused lifecycle test. Browser contexts were disposed
through awaited Playwright fixtures; the manual browser tabs were closed separately.

[Allowlisted acceptance aggregate](evidence/2026-10-29-acceptance.json),
[cleanup observation](evidence/2026-10-29-cleanup.json),
[actual allowlisted review audit sample](evidence/2026-10-29-audit-example.json).
The audit sample was selected read-only during this acceptance run.

## Independent review closeout

The single fresh-context review of 063e2d5 found two authentication defects and one
result-preservation defect. The latter was regraded from Minor to Important because
known failed cases must survive result validation failure. All three regressions were
observed RED, fixed in one pass, then GREEN on 5c4b5be. The full focused suite and all
acceptance groups passed again on that clean commit. No second review was dispatched.
There are no deferred minor findings.

- Routing aliases `/mcp/` and `/MCP/` now receive the same baseline scope check and
  durable unauthenticated/invalid-token audit as the canonical route. Valid canonical
  audience tokens still work; missing-read tokens return 403.
- Audience comparison explicitly rejects trailing-slash differences. The isolated
  signed control accepts the canonical audience and rejects the slash variant; the
  production service never trusts the fixture key.
- Duplicate/unrecognized TRX entries preserve safely observed fail/pass/not_run cases
  and separately fail result validation and overall acceptance.

The [initial measured report](local-report-initial-2026-10-29.md) and its original
allowlisted artifacts remain available as historical evidence. Private failed RED runs
and both timestamped acceptance attempts remain ignored. [Execution rulings and review
limits](execution-notes.md) record what was deliberately left outside this milestone.