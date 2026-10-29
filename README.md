# Delivery Exceptions MCP

A bounded C#/ASP.NET Core portfolio demonstration with three authenticated MCP tools,
six fictional deliveries, two business tenants and three synthetic users. Real browser
PKCE sign-in issues resource-targeted tokens; scopes intersect fixed database membership.
A review checks the caller's expected version and commits its update and audit together.

29 October 2026: **54 focused tests and all 12 acceptance groups passed**, zero skips,
on one clean source/configuration set. See the [dated results and limits](docs/local-report.md).
This is local loopback evidence. Keycloak's pinned resource-indicators feature is
experimental. Remote HTTPS hosting, hosted CI and language-model behavior are unproven.

| Tool | Permission |
|---|---|
| `list_delayed_deliveries` | Read scope and tenant read membership |
| `get_delivery(delivery_id)` | Same, scoped to the caller's tenant |
| `mark_exception_reviewed(delivery_id, expected_version)` | Read/review scopes and review entitlement |

Denied foreign IDs reveal no foreign version or fields. Untrusted carrier text cannot
supply tenant identity or grant permissions. Audit outage prevents data or write success.
No employer code/data, paid calls, cloud resource or other portfolio project is involved.

## Run locally

Use PowerShell 7 on Windows, Ubuntu WSL Docker and available loopback ports 5180-5183.
The [runbook](docs/local-runbook.md) covers prerequisites, private credentials, real browser
sign-in, three-user demonstrations and full evidence reproduction.

```powershell
./scripts/setup.ps1
./scripts/dependencies.ps1 up
# Wait for the issuer discovery URL documented in the runbook.
./scripts/service.ps1 -Initialize
./scripts/service.ps1
```

In another terminal:

```powershell
& ./.local/dotnet/dotnet.exe src/DeliveryExceptions.Client/bin/Release/net10.0/DeliveryExceptions.Client.dll list
& ./.local/dotnet/dotnet.exe src/DeliveryExceptions.Client/bin/Release/net10.0/DeliveryExceptions.Client.dll review --id a0000000-0000-0000-0000-000000000001 --expected-version 1
```

The explicit review command authorizes that one attempt. Exit 0 succeeds, 1 is a business
or permission denial, and 2 is usage/login/operational failure. Stop the foreground
service with Ctrl+C, then run `./scripts/stop.ps1`; named evidence volumes remain.

[Architecture](docs/architecture.md) | [Approved design](docs/superpowers/specs/2026-10-29-delivery-exceptions-mcp-design.md)
