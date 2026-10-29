# Project 18: Delivery-exceptions MCP service

Date: 29 October 2026 (Australia/Brisbane)

Status: approved by Dmitry on 29 October 2026. No product implementation,
identity-provider setup, publication, paid calls or hosted deployment has occurred.

Builder check: a fresh C#/OAuth/MCP reviewer reported "nothing important left" on
29 October 2026. The final local process-layout simplification below is the assistant's
implementation choice within that reviewed scope.

## Intent and decisions

Build a bounded personal CV/portfolio demonstration of modern C#/ASP.NET Core
service development and delegated MCP authorization over synthetic business records.
The story is: "Which of our deliveries are delayed, what happened, and can you mark
this exception as reviewed?"

Dmitry accepted the delivery-exception scenario and delegated the identity-provider
recommendation to the assistant. The assistant recommended local Keycloak first.
Dmitry then approved this written design. The defaults below originated as assistant
proposals and are covered by that approval; they are not additional decisions
independently supplied by Dmitry.

Proposed separate repository: `delivery-exceptions-mcp`, local path
`E:\Projects\delivery-exceptions-mcp`. Keep the design and implementation plan in
`portfolio-plan` initially and copy their approved versions into the new repository.
No changes to ReleaseLens, releaselens-mcp, Fabric or Projects 13-17 are required.

## Goals and bounds

- Authenticate actual synthetic users through an established OAuth/OIDC server.
- Discover and call three tools over MCP Streamable HTTP using a genuine SDK client.
- Demonstrate issuer, signature, audience, lifetime, scope, membership and tenant checks.
- Make one restricted state change with optimistic concurrency and durable audit.
- Retain dated, reproducible HTTP/client evidence and precise limitations.

Outside this milestone: cloud hosting, real carrier integrations, real business records,
shipping operations, financial actions, a frontend dashboard, tenant administration,
custom identity-provider code, paid models, autonomous agents, production availability,
penetration-test certification, compliance and commercial adoption claims.

Failure conditions include cross-tenant disclosure/mutation, permission checks relying
on prompts, accepting another resource's token, treating scope requests as user
entitlements, silent authorization workarounds, excessive infrastructure and overstated
evidence. The initial client is a deterministic MCP demonstration, not an evaluated AI
assistant. An LLM client would be a separately scoped extension.

## Approaches

1. **Local Keycloak, recommended after Dmitry delegated the choice.** Provides real
   sign-in, repeatable synthetic identities and local operation without cloud billing.
   Costs one additional container and explicit provider compatibility work.
2. **Personal Microsoft Entra tenant.** Provides Microsoft-managed identity and an
   Azure-oriented story. Requires app registrations, personal account configuration
   and approval under RULES.md. Not selected for this initial recommendation.

Both would use an established authorization server. Provider interoperability must be
demonstrated, not inferred from a valid JWT alone.

## Components and protocol

- .NET 10 LTS and ASP.NET Core, pinned to an available supported servicing release
  during planning; do not install or upgrade machine-wide tooling without need.
- Official `ModelContextProtocol.AspNetCore` and matching client SDK, initial candidate
  2.3.0. Pin MCP protocol `2026-07-28` and assert the observed protocol in acceptance.
- Stateless Streamable HTTP at canonical resource `http://127.0.0.1:5180/mcp`.
  No server-initiated interactions, task extension or transport session store.
- Keycloak 26.8.1 initial candidate, one dedicated demo realm, pre-registered public
  native client and resource-server client. Use authorization code with PKCE S256.
- PostgreSQL 17, pinned patch/image digest during planning, for application state and
  audit. Npgsql with small parameterized SQL repositories and versioned SQL migrations.
  No ORM or additional application architecture framework is required.
- Docker Compose for PostgreSQL and Keycloak. The ASP.NET Core service and .NET CLI
  client run on the host, keeping issuer discovery and browser callbacks on the same
  loopback network without container-host routing or custom metadata transport.
  Keycloak uses its local development store; no production identity persistence claim.
- xUnit and a small browser sign-in test driver selected during planning. Retain a
  manual interactive client recipe as well as automated acceptance.

Bind the application listener and published Keycloak/database ports strictly to loopback.
Use one stable loopback issuer URL reachable by service, browser and client.
Pin the issuer,
resource URL and redirect URL in configuration rather than deriving them from request
headers. The native redirect is a pre-registered exact loopback URL, with a listener
bound before authorization starts and closed after completion or timeout.

Plain HTTP and development-mode Keycloak are local demonstration choices. They do not
establish HTTPS deployment readiness. Restrict accepted hosts/origins, reject unexpected
origins, and leave CORS disabled for the CLI flow. There is no public exposure/tunnel.

## Provider compatibility and OAuth flow

Documentation checked 29 October 2026 establishes SDK and provider capability, not an
executed compatibility result. In particular, Keycloak labels RFC 8707 resource-indicator
support **experimental**. Enable only `resource-indicators` for this bounded local demo;
record the exact version/feature flag as an explicit limitation. No custom Keycloak
extension, authorization proxy, password grant or token-exchange service is in scope.

Before domain-tool implementation, a planned compatibility task must establish:

1. Initial unauthenticated MCP request returns HTTP 401 with `WWW-Authenticate`
   pointing to the service's RFC 9728 protected-resource metadata. That document is
   readable without a token and identifies the exact resource and trusted issuer.
2. The SDK client discovers provider metadata advertising S256, uses its pre-registered
   client ID and runs browser authorization code plus PKCE. Validate state and issuer
   using the SDK/library flow. Dynamic registration and CIMD are unnecessary here.
3. Authorization and token requests both send `resource` equal to the canonical MCP
   URL. Configure Keycloak's resource-server `resource_url` and client audience mapping
   according to its current resource-indicator instructions.
4. Keycloak issues a token for that resource, rejects an unknown resource and rejects
   changing the resource between authorization and exchange. Tampered state and an
   incorrect PKCE verifier fail. Retain sanitized results, never codes or tokens.
5. ASP.NET Core JWT bearer middleware validates exact issuer, audience, signature with
   discovered trusted keys, required expiry and lifetime. Reject unsigned tokens,
   ID tokens, unsupported signing algorithms and tokens for other resources. Use
   explicit small clock skew and document it. No handwritten cryptography.

If the pinned provider/SDK fails this check, record the failure and stop domain work
at that boundary. A provider/version/design change requires a design amendment, not
disabled validation or a fixed-audience workaround represented as full RFC 8707 support.
The experiment's feature flag is not recommended as a production configuration.

Tokens remain in client process memory only. Do not request offline access or persist
refresh tokens; sign in again when needed. Disable password grants, client credentials
for the demo client and anonymous client registration. Generate demo passwords/admin
credentials locally into ignored private setup material; never commit credentials.

## Identity, scopes and authority

Use one authorization realm and two application business tenants. These business
tenants are not separate Keycloak realms. Seed three users with stable synthetic IDs:

| User | Business tenant | Application permission |
|---|---|---|
| A operator | Retailer A | Read deliveries and review exceptions |
| A reader | Retailer A | Read deliveries only |
| B operator | Retailer B | Read deliveries and review exceptions |

Application membership maps validated `(issuer, subject)` to one tenant and permission
set in PostgreSQL. Missing membership denies access. No email matching, caller-supplied
tenant selector, self-service membership or administration tool is provided.

OAuth scopes are `deliveries.read` and `exceptions.review`. Both are explicitly
configured client scopes. Effective permission is the intersection of token scopes and
the application's fixed user entitlement. A consented/requested review scope cannot
turn A reader into an operator. An operator using a read-only token cannot write.
The SDK client starts with read scope; its review mode explicitly requests both scopes
through another sign-in/consent flow. No automatic privilege escalation/retry on denial.

Middleware establishes authenticated identity. A shared authorization/data-access layer
checks operation scope, membership and tenant for every call. All record reads and
updates include the server-derived tenant in their SQL predicate. Tool arguments and
carrier notes cannot override identity, tenant or permissions. Do not add database RLS
or claim defense against a compromised application/database administrator.

All three tool schemas are discoverable after authenticated read access. Tool visibility
is not authorization. The write tool enforces its own permission on every invocation.

## Records and tool contract

Freeze six small deliveries, three per tenant: one delayed/unreviewed, one
delayed/already-reviewed and one on time. Store explicit synthetic status; do not depend
on today's clock to decide lateness. One delayed A delivery includes an adversarial
carrier note referencing a B delivery ID.

Minimal tables: tenants, memberships, deliveries and audit. A delivery includes ID,
tenant, fictional reference, expected/observed timestamps, status, bounded carrier note,
reviewed flag, reviewer/time and integer version. Source delivery fields and notes are
immutable through the public tool surface. There are no importer or free-text write APIs.

| Tool | Arguments | Result and authority |
|---|---|---|
| `list_delayed_deliveries` | None | All delayed deliveries for the current tenant, including review flag/version. Fixed fixtures keep the list small. Requires read scope/permission. |
| `get_delivery` | `delivery_id` | Tenant-owned delivery details, version and explicitly untrusted carrier note. Requires read scope/permission. |
| `mark_exception_reviewed` | `delivery_id`, `expected_version` | Changes one delayed, unreviewed exception to reviewed and increments version. Requires both scopes and operator permission. |

The write records the authenticated reviewer and server UTC time. It does not change
shipment status or carrier data. The explicit CLI review command is the user's decision;
there is no separate pending-approval workflow or approval UI.

Implement the write as one transaction: tenant-scoped conditional update plus success
audit commit atomically. Stale versions, already-reviewed records and on-time deliveries
produce a conflict with no mutation. Two concurrent writes using the same version yield
one success and one conflict. A repeated old-version request is a conflict; do not build
an idempotency-key platform. Unauthorized and foreign/absent records are checked before
exposing versions or record state. Foreign and nonexistent IDs have indistinguishable
not-found responses and reveal no owning tenant.

## Failures, audit and untrusted text

- Missing/invalid/expired credentials fail at the HTTP boundary with 401 and a valid
  resource challenge. No tool executes. Missing baseline read scope gets HTTP 403 with
  an insufficient-scope challenge.
- Authenticated write-scope/entitlement denials use the SDK-supported HTTP scope challenge
  when available before dispatch; otherwise return a stable MCP tool error. Specify and
  test the actual SDK mapping during planning. Never claim a tool error was HTTP 403.
- Tenant lookups, business conflicts and validation failures use stable structured MCP
  tool errors: not found, conflict and invalid argument. No raw SQL/exception detail.
- The CLI reports outcome, record/version when permitted and a correlation ID. It never
  prints tokens, authorization codes, request headers or passwords.

Audit bounded fields: correlation ID, UTC timestamp, validated synthetic subject/tenant
when known, operation, requested synthetic ID, allow/deny/conflict reason and versions
when authorized. Invalid-token events have no trusted subject/tenant, even if their
unverified JWT contains identity claims. Do not store token text or complete requests.
Transport-level rejections use an authentication-event operation because the tool body
need not be trusted/parsed. Redact carrier-note contents and credentials from logs.

Persist denied-call audit separately from rolled-back domain transactions. If audit
storage is unavailable, reject business execution; the database cannot guarantee an
audit of that outage. A sanitized operational error is the explicit limitation. Success
must never be reported without its atomic state/audit commit.

Carrier notes appear as clearly identified untrusted data. The injection demonstration
reads the malicious note, then deliberately attempts the forbidden cross-tenant write
and a reader write through the actual client. Neither may mutate state. This proves the
service boundary resists those requests; it does not measure an LLM's susceptibility
or establish general prompt-injection safety.

## Acceptance and evidence

Keep one small acceptance suite plus focused unit checks. Run against the actual
host service, Compose dependencies, real Keycloak-issued tokens, real PostgreSQL and
the SDK HTTP client.
Automated browser login must use the same authorization-code flow as the interactive
client; do not substitute password/client-credentials grants for convenience.

| Case | Required observed result |
|---|---|
| A1 Discovery/login | 401 challenge, metadata, PKCE browser sign-in and pinned protocol; authenticated tool discovery succeeds. |
| A2 Read isolation | A operator/reader see exactly A's delayed fixtures; B operator sees exactly B's. |
| A3 Foreign/absent read | Known foreign ID and missing ID give the same not-found shape and reveal no foreign fields. |
| A4 Permitted write | A operator with both scopes reviews one A exception; version increases once and audit matches committed state. |
| A5 Scope/entitlement | Operator read-only token cannot write; reader cannot write even if requesting review scope. No mutation in either case. |
| A6 Foreign write | A operator with write scope cannot change B's exception; it remains byte-for-byte unchanged in selected domain fields. |
| A7 Token rejection | Absent, malformed, expired, tampered, unsigned, wrong-issuer, wrong-audience and ID tokens fail actual HTTP requests before execution. |
| A8 Concurrency/domain | Stale/repeated version, on-time and already-reviewed writes conflict; two same-version concurrent calls yield one mutation. |
| A9 Injection boundary | Malicious carrier note is returned as data; attempted unauthorized writes fail and both tenants' forbidden records remain unchanged. |
| A10 Audit/privacy | Success and denial events persisted with correct trusted identity rules; retained client/server artifacts contain no credentials/tokens. |
| A11 Persistence | Restart application with PostgreSQL volume retained; reviewed state/version and audit remain; fresh sign-in still works. |
| A12 OAuth target binding | Unknown/changed resource, wrong PKCE verifier and mismatched callback state fail; valid resource/audience path succeeds. |

Most cases use real provider-issued tokens, including actual expiry under a short
test lifetime without editing token claims. Wrong issuer/audience/signature cases may
use isolated test keys/fixtures; label their provenance. The deployed service never
trusts those keys and never has a test-auth bypass. Reset fixtures only through private
test setup between independent cases, never through a public reset tool.

Report date, source SHA, fixture digest, exact versions/configuration, passed/failed/
not-run cases, sample counts and observed limitations. Retain machine-readable
sanitized results, a short terminal recording/transcript, redacted audit examples,
architecture/setup documentation and a manual reproduction recipe. No throughput,
AI-quality or comprehensive security claim is required. HTTP token-validation tests
alone do not substitute for successful real browser/provider/client acceptance.

## Delivery boundary and completion

After design approval, create a short implementation plan starting with the compatibility
task. Candidate sequence: OAuth compatibility, domain/authorization, audit/client,
acceptance and report. Aim for one small service and test client, not reusable platform
infrastructure. Stop after the bounded evidence is complete. If compatibility fails,
retain that result and leave the milestone incomplete pending a design amendment.

Local completion requires all selected acceptance cases passing and accurately reported,
repeatable setup and verified stop of the project-owned application, identity-provider,
database and temporary browser/listener processes. Retain ignored database evidence
volumes unless Dmitry requests removal. Do not claim stopped local containers establish
cloud teardown. Update Project 18 tracker/handover with actual outcomes.

Public repository creation/push and visibility require Dmitry's approval under RULES.md.
Use the personal commit identity. No Azure commands, Entra registrations, access grants,
cloud provisioning or paid evaluations belong to this local design. A future brief
HTTPS milestone needs its own concrete host, identity configuration, cost/budget,
approval and teardown plan. Until measured, describe this as a locally demonstrated
authenticated HTTP MCP service, with remote hosting unproven.

Builder discretion: project/module layout, SQL migration runner, exact supported patch
versions and image digests, browser test library, private fixture-setup mechanics and
sanitized report formatting. Decisions that weaken authorization or broaden scope need
a design amendment.

## Primary references checked 29 October 2026

- [MCP authorization, revision 2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization).
- [Official C# SDK transports](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/transports/transports.md).
- [Official ASP.NET Core SDK package, 2.3.0](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore/2.3.0).
- [Keycloak MCP integration and experimental resource indicators](https://www.keycloak.org/securing-apps/mcp-authz-server).
- [Keycloak Docker quickstart](https://www.keycloak.org/getting-started/getting-started-docker).
- [Keycloak OIDC endpoints and flows](https://www.keycloak.org/securing-apps/oidc-layers).
- [Alternative: Entra MCP integration](https://learn.microsoft.com/en-us/entra/agent-id/secure-mcp-server-with-entra-id).

These sources support the design choices. No local interoperability, authorization
acceptance or hosting measurement has yet been taken.
