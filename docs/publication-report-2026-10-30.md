# Publication and hosted CI - 30 October 2026

Dmitry approved creating the public repository, pushing the reviewed project and
running hosted CI on 30 October 2026 (Australia/Brisbane). The public repository is
[delivery-exceptions-mcp](https://github.com/DmitryLyubaev/delivery-exceptions-mcp),
with `main` as its default branch.

[GitHub Actions run 40937981554](https://github.com/DmitryLyubaev/delivery-exceptions-mcp/actions/runs/40937981554)
passed on exact source `c765658f4d7c3f6cca1a827e25c6e153728b374b`, tree
`873b98f9e54bf490f9f0c6eaee44001a21a0d926`. The service, test, script and infrastructure
inputs match the reviewed corrective source `5c4b5be`; publication preparation only
masked generated runner credentials and added an allowlist for result uploads.

## Observed result

One hosted run, Ubuntu 24.04 image `20261025.353.1`, runner `2.338.0`. Job duration was
3 minutes 26 seconds, from 05:12:41 to 05:16:07 Brisbane on 30 October (19:12:41 to
19:16:07 UTC on 29 October). This is a single functional sample, not a performance benchmark.

| Check | Actual result |
|---|---|
| Locked restore, formatting, Release build | pass, zero build warnings/errors |
| Focused tests | 54 passed, zero failed/skipped; reported duration 45 seconds |
| A1-A12 acceptance | all twelve pass; result validation and overall acceptance true |
| Owned cleanup | pass, zero demo listeners and zero running demo containers |
| Evidence volumes | two retained on the runner until its normal disposal |
| Repository controls | public; secret scanning and push protection enabled |

The run exercised browser PKCE and resource targeting, genuine token expiry and
ID-token rejection, labelled signature/issuer/audience controls, route-alias scope and
audit checks, tenant isolation, fixed entitlement, atomic review/audit, concurrency,
malicious-note boundary and application restart. There were no language-model
assessment calls or application cloud deployments.

## Evidence and provenance

The artifact contained exactly `check.json`, `acceptance.json`, `cleanup.json` and
`transcript.txt`. Copies are committed under [hosted evidence](evidence/hosted-40937981554/),
with a [workflow metadata record](evidence/hosted-40937981554/workflow-run.json).
Its GitHub artifact digest was
`sha256:b2633b35d149d1e272b1b6e5e4476bd227a97fe8f39fab359b4b13b3e7b45784`.
The original hosted artifact expires after seven days; the committed safe copies remain.
No private credentials, generated realm/environment, raw TRX, browser or provider traces
were uploaded. Pre-publication selected-pattern and exact-credential scanning covered
120 historical blobs across 13 commits, with zero matches and personal Git identity only.

The Linux artifact's source/tree, fixture-manifest digest and configuration-set digest
were independently checked against committed Git bytes. The frozen delivery/membership
fixture bytes match their manifest. Manifest/configuration hashes differ from the Windows
report because working-file line endings differ; the two measurements retain their own
identities instead of treating their raw-byte hashes as interchangeable.

The acceptance generator always describes loopback application tests and does not
self-attest GitHub hosting: `environment=local-loopback` and `hostedCiProven=false`
remain in its JSON. Hosted execution is established separately by the GitHub run/job
identity and downloaded artifact bound to its source SHA. These facts do not establish
a remotely deployed HTTPS MCP service.

## Limits and non-failing notices

The pinned Keycloak resource-indicators feature remains experimental. The workflow ran
on Ubuntu 24.04; other runner images, production identity/availability, remote HTTPS and
model robustness remain unproven. The earlier [Windows report](local-report.md) and
[initial pre-review report](local-report-initial-2026-10-29.md) remain dated evidence.

GitHub emitted non-failing notices about Node 20 actions being forced onto Node 24 and
migration of the ubuntu-latest label. The measured runner image above is the observed
image, not a claim about every future hosted run. Local evidence containers remain
stopped with their named volumes retained. The previously blocked ignored-scratch
deletion was not retried during publication.
