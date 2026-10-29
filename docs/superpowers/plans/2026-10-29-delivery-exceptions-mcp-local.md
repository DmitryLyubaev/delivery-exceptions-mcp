# Delivery Exceptions MCP Local Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Demonstrate three authenticated C# MCP tools over synthetic deliveries, with real browser sign-in, enforced scope/tenant permissions and retained local acceptance evidence.

**Architecture:** Run the ASP.NET Core service and SDK CLI on the host, with Keycloak and PostgreSQL in loopback-published Compose containers. JWT middleware authenticates callers; application services intersect OAuth scopes with database membership and use tenant-scoped SQL. Prove actual SDK/provider interoperability before implementing business tools.

**Tech Stack:** .NET 10, ASP.NET Core JWT bearer middleware, MCP C# SDK 2.3.0, Keycloak 26.8.1, PostgreSQL 17, Npgsql 10.0.3, xUnit and Microsoft.Playwright browser sign-in tests.

**Spec:** [Approved Project 18 design](../specs/2026-10-29-delivery-exceptions-mcp-design.md), approved by Dmitry on 29 October 2026. Read the design and `RULES.md` before execution.

**Status:** Approved by Dmitry on 29 October 2026 for Native execution. Local implementation and acceptance passed; final independent branch review is pending.

## Global Constraints

- Build a bounded personal CV/portfolio demonstration of modern C#/ASP.NET Core service development and delegated MCP authorization over synthetic business records.
- No changes to ReleaseLens, releaselens-mcp, Fabric or Projects 13-17 are required.
- Pin MCP protocol `2026-07-28` and assert the observed protocol in acceptance.
- Stateless Streamable HTTP at canonical resource `http://127.0.0.1:5180/mcp`.
- OAuth scopes are `deliveries.read` and `exceptions.review`.
- Effective permission is the intersection of token scopes and the application's fixed user entitlement.
- No email matching, caller-supplied tenant selector, self-service membership or administration tool is provided.
- Tokens remain in client process memory only.
- No automatic privilege escalation/retry on denial.
- No custom Keycloak extension, authorization proxy, password grant or token-exchange service is in scope.
- A provider/version/design change requires a design amendment, not disabled validation or a fixed-audience workaround represented as full RFC 8707 support.
- No Azure commands, Entra registrations, access grants, cloud provisioning or paid evaluations belong to this local design.
- Public repository creation/push and visibility require Dmitry's approval under RULES.md.
- Use `Dmitry Lyubaev <Dmitry.Lyubaev@gmail.com>` for commits and ASCII hyphens in prose. Never print credentials or tokens.

## Review Focus

1. Occupied callback/service ports and wrong callback paths fail safely without stopping another process. Task 1 tests this.
2. Duplicate/malformed identity claims and substring scope names cannot select another membership or grant access. Task 2 tests this.
3. Invalid versions, oversized IDs and extra tenant arguments produce bounded errors without disclosure or mutation. Task 3 tests this.
4. Audit-storage failure prevents business success; cancellation closes the callback listener without leaking secrets. Tasks 3-4 test this.
5. Partial acceptance retains failures/not-run cases and cleans up only owned processes/containers. Task 5 tests this.

---

## Environment and execution boundary

Create `E:\Projects\delivery-exceptions-mcp`, branch `codex/local-demo`, only when
execution starts. Apply using-git-worktrees then; the new repository provides isolation
from existing products. Copy the approved design and reviewed plan into its
`docs/superpowers/` directories. No GitHub repository, push or visibility change.

Read-only checks on 29 October found Windows SDK 10.0.201/runtime 10.0.5, no Windows
Docker command, and Ubuntu WSL Docker 29.7.2 with Compose v5.4.0. The product directory
is absent. Windows-to-WSL loopback reachability has not yet been demonstrated.

Use project-local SDK **10.0.403**, targeting `net10.0`, with ASP.NET Core/runtime
**10.0.14**, as recorded in Microsoft's release metadata on 29 October. Verify download
checksums against that metadata. Do not modify machine PATH, installed SDKs or global
runtime configuration. If unavailable, record readiness failure rather than silently
changing versions. Invoke the project-local dotnet executable explicitly; commands
below use `dotnet` as shorthand for it. Never repurpose HOME or CODEX_HOME variables.

Task 1 records direct/test NuGet versions in `Directory.Packages.props` and per-project
`packages.lock.json`. Record immutable Keycloak 26.8.1 and maintained PostgreSQL 17 patch
image digests in `infra/versions.json` before use. Resolve test dependencies once and
use locked restore afterward. Install matching Playwright Chromium into ignored
`.local/` only. No machine-wide browser installation or global tooling upgrade.

Compose project is `p18-delivery-exceptions`; manage only its labelled resources.
Publish Keycloak at `127.0.0.1:5182` and PostgreSQL at `127.0.0.1:5183`. Host service
uses 5180; exact native callback is `http://127.0.0.1:5181/callback`. Issuer is
`http://127.0.0.1:5182/realms/delivery-demo`. Refuse occupied ports without killing
their owners. Bind explicitly to IPv4 loopback and reject unexpected hosts/origins.

Use Windows dotnet and `wsl -d Ubuntu -- docker compose` with explicit WSL-converted
paths locally; CI/Linux can use native Docker. Verify host access to both dependencies
before OAuth tests. Do not modify WSL networking, firewall, Windows features or Docker
daemon configuration. Never run global Docker prune/stop or delete evidence volumes.

## File map and shared contracts

Paths below are relative to the new product root.

| Paths | Responsibility |
|---|---|
| `DeliveryExceptions.sln`, `global.json`, `Directory.{Build,Packages}.props`, `NuGet.config`, `.gitignore`, `.gitattributes` | Locked tooling, safe defaults and byte-preserving fixtures |
| `infra/{compose.yaml,versions.json,keycloak/realm-template.json}` | Two local dependencies and secret-free provider configuration |
| `scripts/{setup,dependencies,check,acceptance,stop}.ps1` | Private setup, owned lifecycle, checks and evidence |
| `src/DeliveryExceptions.Service/{Program.cs,Configuration.cs}` | Service composition, fixed URLs and JWT/MCP setup |
| `src/DeliveryExceptions.Service/Auth/{Actor.cs,AccessPolicy.cs,TransportAudit.cs}` | Validated caller, scopes/membership and HTTP denial audit |
| `src/DeliveryExceptions.Service/Deliveries/{Contracts.cs,DeliveryRepository.cs,DeliveryService.cs,DeliveryTools.cs}` | Typed records, tenant SQL, application operations and three tool adapters |
| `src/DeliveryExceptions.Service/Storage/{Database.cs,AuditStore.cs,Migrations/001_initial.sql}` | Explicit migrations, connections and audit persistence |
| `src/DeliveryExceptions.Client/{Program.cs,OAuthLogin.cs,ClientCommands.cs}` | Browser PKCE, callback listener and deterministic CLI |
| `fixtures/{deliveries.json,memberships.json,manifest.json}` | Six independent records, three synthetic identities and byte digests |
| `tests/DeliveryExceptions.Tests/{OAuthCompatibilityTests.cs,AccessPolicyTests.cs,DeliveryRepositoryTests.cs,DeliveryToolsTests.cs,AuditTests.cs,ClientTests.cs,AcceptanceTests.cs,ReportTests.cs}` | Focused unit, PostgreSQL, SDK HTTP and provider tests |
| `tests/DeliveryExceptions.Tests/Support/{DemoStack.cs,BrowserLogin.cs,TokenFixtures.cs}` | Owned processes, real browser login and labelled negative tokens |
| `.github/workflows/ci.yml`, `README.md`, `docs/{architecture,local-runbook,local-report}.md` | Unpublished CI definition, reproduction and measured limits |

Do not add a contracts library, generic repository framework or custom OAuth library.
Add focused helper files only when a named behavior needs them.

Task 2 defines these shared records (constructor signatures shown):

- `Actor(string Issuer, string Subject, IReadOnlySet<string> Scopes)` contains validated claims only.
- `Membership(Guid TenantId, bool CanRead, bool CanReview)` is resolved by exact issuer/subject.
- `DeliveryView(Guid Id, string Reference, string Status, string CarrierNote, bool Reviewed, long Version, string? ReviewedBy, DateTimeOffset? ReviewedAt)` contains permitted delivery fields.
- `ToolOutcome<T>(T? Value, string? ErrorCode, string CorrelationId)` contains exactly one value or error; codes are `forbidden`, `not_found`, `conflict`, `invalid_argument`, `unavailable`.
- `AuditEvent(string CorrelationId, DateTimeOffset At, string? Issuer, string? Subject, Guid? TenantId, string Operation, Guid? DeliveryId, string Outcome, string Reason, long? BeforeVersion, long? AfterVersion)` contains no raw inputs or secrets.

Use `CancellationToken` for asynchronous APIs. Correlation IDs are server-generated,
never an arbitrary echoed header. IDs are GUIDs; versions positive `long`; notes at
most 1,000 characters and references at most 40. Reject unknown tool arguments,
including `tenant_id`. Scope parsing matches exact space-delimited values. No raw
SQL/exception messages enter public results. Label carrier notes as untrusted data.

## Task 1: Real OAuth and SDK compatibility

**Files:** Create tooling/configuration, `infra/`, setup/dependencies/stop scripts,
service `Program.cs`/`Configuration.cs`, client `OAuthLogin.cs`, test runner,
`OAuthCompatibilityTests.cs` and `Support/{DemoStack,BrowserLogin}.cs`.

**Interfaces:** Produce `OAuthLogin.ConnectAsync(Uri resource, bool requestReview,
CancellationToken ct) -> Task<McpClient>` using pre-registered `delivery-demo-cli`.
`DemoStack.StartAsync(CancellationToken ct) -> Task<DemoStack>` owns dependency/process
handles and implements `IAsyncDisposable`. No business tools yet; an authenticated
empty tool listing suffices for this gate.

- [x] **Step 1:** Verify path isolation/Git identity, create the local repository and minimal solution/test runner. Fetch project-local tooling, resolve exact package/image pins, generate ignored credentials and import one dedicated realm. Disable self-registration, direct grants, service-account grants and anonymous registration. Enable only `resource-indicators`; no CIMD feature. Record actual readiness privately without credentials.
- [x] **Step 2:** Write `ChallengeAdvertisesExactResource`, `BrowserPkceLoginDiscoversEmptyToolList`, `UnknownResourceFails`, `ChangedResourceAtExchangeFails`, `WrongVerifierFails`, `MismatchedStateFails`, `WrongCallbackPathFails`, `OccupiedCallbackPortFailsWithoutStoppingOwner` and `UnexpectedHostOrOriginRejected`. Assert exact issuer/resource, S256, HTTP 401/challenge, protocol `2026-07-28`, no exchange after rejected callback state and no termination of another process.
- [x] **Step 3:** Run `dotnet test tests/DeliveryExceptions.Tests --filter Category=Compatibility`; inspect expected missing-behavior failures. Connectivity/discovery failures are readiness/compatibility findings, not useful RED assertions or passing skips.
- [x] **Step 4:** Implement JWT bearer plus SDK `AddMcp` metadata and stateless HTTP using the official v2.3.0 APIs. Configure Keycloak `resource_url`, resource-server audience mapper and optional scopes. `OAuthLogin` uses SDK `ClientOAuthOptions`, pre-registration, `AuthorizationCallbackHandler` and `AuthorizationResult`, preserving SDK state/issuer checks. Use a temporary Kestrel callback listener, bound before browser launch, rather than machine URL-ACL changes. Normal token lifetime 120 seconds; clock skew two seconds; signing algorithm RS256. Require nonempty subject, expiry and access-token type; reject ID tokens and wrong audience. Keep legacy metadata fallback disabled.
- [x] **Step 5:** Repeat Compatibility against the real browser/provider, require all checks GREEN, and retain sanitized observations of resource in both requests, provider target binding and SDK protocol. If any requirement fails, record it and stop before domain work pending a design amendment. Never disable validation or substitute fixed audience behavior to pass.
- [x] **Step 6:** Record pins and actual compatibility results in `docs/local-runbook.md`, stop owned dependencies between sessions, and commit exact task files as `feat: prove local MCP OAuth compatibility`.

Automated sign-in uses the same SDK/browser flow as the interactive CLI. Never obtain
test-user tokens using password/client-credentials grants. Do not log authorization
URLs, callback queries, codes, PKCE verifiers or tokens.

## Task 2: Membership and tenant-scoped PostgreSQL reads

**Files:** Create `Auth/{Actor,AccessPolicy}.cs`, `Deliveries/{Contracts,
DeliveryRepository,DeliveryService}.cs`, `Storage/{Database,AuditStore}.cs`, migration,
fixture files, `AccessPolicyTests.cs` and `DeliveryRepositoryTests.cs`.

**Interfaces:** Produce `AccessPolicy.ResolveAsync(Actor actor, bool review,
CancellationToken ct) -> Task<Membership?>`; `DeliveryRepository.ListDelayedAsync(Guid
tenantId, CancellationToken ct) -> Task<IReadOnlyList<DeliveryView>>` and
`GetAsync(Guid tenantId, Guid id, CancellationToken ct) -> Task<DeliveryView?>`.
`AuditStore.AppendAsync(AuditEvent entry, NpgsqlConnection connection,
NpgsqlTransaction? transaction, CancellationToken ct) -> Task` supports the caller's
transaction. `Database.MigrateAsync(CancellationToken ct) -> Task` runs versioned SQL;
normal startup never resets fixtures.

- [x] **Step 1:** Author A record IDs `a0000000-0000-0000-0000-000000000001` through `...0003`, and equivalent B IDs starting `b0000000`. Per tenant: delayed/unreviewed version 1, delayed/reviewed version 2, on-time/unreviewed version 1. A's first note instructs changing B's first record. Freeze fixture bytes/digests. Seed the same three stable subjects in Keycloak and membership fixtures: A operator, A reader, B operator.
- [x] **Step 2:** Write `ReadersSeeOnlyOwnTenant`, `KnownForeignAndMissingHaveSameResult`, `MissingMembershipDenied`, `ReaderCannotAcquireOperatorEntitlement`, `OperatorReadScopeCannotReview`, `MalformedOrDuplicateIdentityDenied`, `ScopeNamesRequireExactMatch`, `OrdinaryStartupDoesNotReseed`. Assert two delayed results per tenant and `deliveries.read.extra` grants no read access.
- [x] **Step 3:** Run `dotnet test tests/DeliveryExceptions.Tests --filter 'Category=Policy|Category=Database'`; inspect behavior-specific RED, using actual PostgreSQL for repository tests.
- [x] **Step 4:** Implement tenants/memberships/deliveries/audit tables, parameterized tenant predicates, strict actor extraction and exact issuer/subject membership. Require read scope plus CanRead; review additionally needs review scope plus CanReview. Keep membership/setup writes private. Implement audit persistence now so Task 3 can commit review plus audit atomically.
- [x] **Step 5:** Repeat Policy/Database to GREEN, verify fixture hashes and unchanged state across service restart, then commit exact files as `feat: add tenant-scoped delivery access`.

## Task 3: Three tools and one atomic reviewed transition

**Files:** Create `Deliveries/DeliveryTools.cs`; extend `DeliveryService.cs`,
`DeliveryRepository.cs` and service composition; add `DeliveryToolsTests.cs` and
transaction tests to `DeliveryRepositoryTests.cs`.

**Interfaces:** Produce `DeliveryService.ListAsync(Actor actor, CancellationToken ct)
-> Task<ToolOutcome<IReadOnlyList<DeliveryView>>>`, `GetAsync(Actor actor, Guid id,
CancellationToken ct) -> Task<ToolOutcome<DeliveryView>>` and
`ReviewAsync(Actor actor, Guid id, long expectedVersion, CancellationToken ct)
-> Task<ToolOutcome<DeliveryView>>`. MCP arguments are exactly the design's three
tool schemas; no tool accepts identity, permission or tenant arguments.

- [x] **Step 1:** Write real-SDK HTTP tests `ThreeToolsHaveNoTenantArgument`, `OperatorReviewsOwnDelayedRecord`, `ReaderAndReadOnlyTokenCannotWrite`, `ForeignWriteIsNotFound`, `ForeignConflictDoesNotExposeVersion`, `SameVersionConcurrentWritesYieldOneSuccess`, `RepeatedOrStaleReviewConflicts`, `OnTimeAndReviewedConflict`, `InvalidVersionOrOversizedIdRejected`, `ExtraTenantArgumentRejected`, `AuditInsertFailureRollsBackReview`. Assert one version increment, authenticated reviewer, UTC timestamp and one committed success audit; verify database state after denials.
- [x] **Step 2:** Run `dotnet test tests/DeliveryExceptions.Tests --filter Category=Tools`; confirm useful RED failures.
- [x] **Step 3:** Implement tenant-scoped conditional update plus success audit in one transaction, authorizing before protected-state reads. Foreign/missing errors have the same shape. Roll back on audit failure. Register exactly `list_delayed_deliveries`, `get_delivery`, `mark_exception_reviewed`, with bounded structured outputs and untrusted-note labelling.
- [x] **Step 4:** Set the design's permitted error-mapping choice: baseline read-scope failure is HTTP 403 with `insufficient_scope`; write scope/entitlement denial is MCP `CallToolResult.IsError=true` with JSON code `forbidden`. Not-found/conflict/invalid-argument use structured MCP tool errors. Test HTTP status separately from MCP body; no automatic reauthorization on denial.
- [x] **Step 5:** Run Tools/Database/Policy to GREEN, inspect the exact tool list and arguments, then commit as `feat: expose authorized delivery review tools`.

## Task 4: Denial audit and deterministic CLI

**Files:** Create `Auth/TransportAudit.cs`, client `Program.cs`/`ClientCommands.cs`;
extend `OAuthLogin.cs`, audit/error wiring and `AuditStore.cs`; add `AuditTests.cs`,
`ClientTests.cs` and `Support/TokenFixtures.cs`.

**Interfaces:** `ClientCommands.RunAsync(string[] args, CancellationToken ct)
-> Task<int>` supports `list`, `get --id <guid>`, and
`review --id <guid> --expected-version <positive-long>`. Read commands request read
scope; review explicitly requests both. Exit 0 succeeds, 1 means rejected business/
permission action, 2 means invalid usage/login cancellation/operational failure.
`TransportAudit.RecordAsync(AuditEvent entry, CancellationToken ct) -> Task` persists
HTTP denial events without trusting unverified JWT identity.

- [x] **Step 1:** Write `AuthenticationDenialHasNoTrustedSubject`, `DenialSurvivesDomainRollback`, `AuditUnavailablePreventsReadAndWriteSuccess`, `TransportAndToolFailuresRemainDistinct`, `CliShowsCorrelationWithoutToken`, `CancelledLoginClosesListener`, `CallbackTimeoutClosesListener`, `LogOutputContainsNoTokenCodeOrPassword`, `CarrierNoteIsNeverAuthority`. Inspect persisted fields and captured stdout/stderr using JWT/secret/control-character markers.
- [x] **Step 2:** Run `dotnet test tests/DeliveryExceptions.Tests --filter 'Category=Audit|Category=Client'`; inspect RED behavior.
- [x] **Step 3:** Implement allowed-read/denied-tool audits separately from rolled-back domain transactions. Audit must succeed before returning read data. Authentication events have no unverified subject; validated callers can carry trusted identity. Audit outage yields sanitized unavailable behavior and prevents business success, without claiming a database audit of that outage. Never store notes, request bodies or headers.
- [x] **Step 4:** Implement CLI commands, explicit review/version arguments, memory-only tokens, no automatic scope escalation and a 120-second callback timeout/cancellation. Print structured outcome and correlation ID. Add the manual three-user browser recipe and deliberate forbidden tool calls after reading the malicious note. No LLM is evaluated.
- [x] **Step 5:** Repeat Audit/Client/Tools to GREEN, exercise actual interactive browser login plus one read/review, retain a sanitized transcript, then commit as `feat: add audited MCP client demonstration`.

## Task 5: Acceptance and evidence closeout

**Files:** Create `AcceptanceTests.cs`, `ReportTests.cs`, check/acceptance scripts,
unpublished CI YAML and architecture/runbook/report docs; extend owned lifecycle
helpers only as needed. Update Project 18 records in `portfolio-plan` after results,
preserving other agents' changes.

**Interfaces:** `scripts/check.ps1` runs locked restore, format check, Release build
and unit/database tests; any failure exits nonzero. `scripts/acceptance.ps1` runs
A1-A12 and writes ignored `.local/results/{acceptance.json,transcript.txt,cleanup.json}`.
Each case includes ID, source/fixture/config identity, provenance and
`pass|fail|not_run`. Failed/not-run cases or failed cleanup produce nonzero exit.
Missing prerequisites are never passing skips. `stop.ps1` stops only owned resources
and preserves PostgreSQL evidence volumes.

- [x] **Step 1:** Write `AllTwelveCasesRequired`, `PartialRunKeepsFailureAndNotRun`, `CleanupDoesNotTouchUnownedProcessOrComposeProject`, `PublishedProjectionHasNoSecrets`, `ReportCannotClaimRemoteOrModelAcceptance`. Assert missing/duplicate case IDs and source/config mismatch fail; an injected mid-run failure still invokes cleanup; independent process/container fixtures survive.
- [x] **Step 2:** Run `dotnet test tests/DeliveryExceptions.Tests --filter Category=Report` to RED, implement strict aggregation and owned cleanup, then repeat to GREEN. Preserve failed attempts rather than rewriting their results.
- [x] **Step 3:** Implement all design A1-A12 against the real service, PostgreSQL and browser-issued tokens. A7 includes genuine expiry using a pre-registered short-lived test client (10 seconds plus two-second skew), genuine ID-token rejection and labelled crafted issuer/audience/signature negatives; production configuration never trusts fixture keys. A8 checks concurrent committed state, A9 reads the note then attempts forbidden writes, A10 inspects logs/audit, A11 restarts only the service, A12 tests actual target binding and PKCE/state failure. Private setup resets case fixtures, never a public reset tool.
- [x] **Step 4:** Run `pwsh -File scripts/check.ps1`, then `pwsh -File scripts/acceptance.ps1`. Require all A1-A12 and build/format/tests GREEN on the same source/fixture/config set. Verify sanitized artifacts and shutdown of owned application, containers, browser and callback listeners. Keep named evidence volumes. Record actual test counts.
- [x] **Step 5:** Write dated report with exact SHA/tree, fixture hash, runtime/SDK/provider/database/browser pins, experimental feature flag, actual statuses, redacted audit, process-stop proof and limits. Include Windows/WSL reproduction and native-Docker CI recipe. CI definition uses no cloud credentials; hosted CI remains unproven until separately published/executed.
- [ ] **Step 6:** Run one whole-branch review under the selected workflow, reproduce/fix material findings and repeat affected checks; rerun final acceptance when behavior/config changed. Update tracker/handover with actual outcomes and outward-action boundary. Commit reviewed source/report and verify personal identity before requesting public publication.

## Coverage and self-review

| Acceptance | Owning tasks |
|---|---|
| A1 Discovery/login; A12 target binding | 1, repeated in 5 |
| A2 read isolation; A3 foreign/absent | 2-3, repeated in 5 |
| A4 permitted write; A5 scope/entitlement; A6 foreign write; A8 conflicts/concurrency | 2-3, repeated in 5 |
| A7 token rejection | 1 and 4-5 |
| A9 injection boundary; A10 audit/privacy | 3-5 |
| A11 persistence | 2 and 5 |

Self-review: all design acceptance and lifecycle requirements map to tasks. All five
Review Focus cases have named tests. Shared method/type names agree. Configuration
and scaffolding are part of the compatibility deliverable, not separate platform work.
No cloud, model study, production hardening or cross-project integration was added.

Recommended execution is **Native**: five sequential tasks in this chat with one
independent whole-branch review. Identity, data and client interfaces are tightly
connected, making sequential execution suitable for this bounded demo. Subagent-driven
execution would add independent implementation/review per task. Dmitry approved the
plan and recommended Native method on 29 October 2026.

Primary references read 29 October 2026: [SDK server sample](https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.3.0/samples/ProtectedMcpServer/Program.cs),
[SDK client sample](https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.3.0/samples/ProtectedMcpClient/Program.cs),
[Keycloak MCP setup](https://www.keycloak.org/securing-apps/mcp-authz-server),
[.NET release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json),
[Npgsql package versions](https://api.nuget.org/v3-flatcontainer/npgsql/index.json).
These are planning references, not local acceptance evidence.
