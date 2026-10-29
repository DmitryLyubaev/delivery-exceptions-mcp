# Execution notes - 29 October 2026

Native execution implemented the approved five tasks with one independent whole-branch review.
Review head063e2d5, corrective source5c4b5be. Three material findings reproduced RED,
fixed in one pass, target3/3 GREEN, final54/54 focused tests and12/12 acceptance groups.
No re-review; no deferred minor findings. The source-bound measured report gives limits.

## Rulings and costs

The approved new repository supplies isolation without another worktree. If wrong,
change isolation would be weaker. Unknown resources may be rejected at exchange,
rather than before browser login: no token is issued; if wrong, rejection is delayed.
Initialization/test budgets were raised to150/180 seconds after79-second cold navigation,
while callback120 seconds and token checks/lifetimes stayed fixed; cost is slower failure feedback.

- remote HTTPS/cloud teardown stays outside this local milestone - approved loopback scope - cost if wrong: remote security and teardown behavior unproven.
- hosted CI/Linux execution remains unproven - only a disclosed unpublished workflow exists - cost if wrong: portability failures may remain.
- model robustness/general injection resistance is not accepted - deliberate requests prove deterministic authority only - cost if wrong: no model-behavior guarantee.
- production scale/rate limits/availability/backup/disaster recovery excluded - six-record portfolio scope - cost if wrong: no operational resilience guarantee.
- membership administration/revocation races excluded - membership is fixed with no administration surface - cost if wrong: immediate revocation not guaranteed.
- compromised administrators/hostile same-user machine control excluded - local trust boundary - cost if wrong: privileged/local compromise can defeat this demo.
- deployed-product migration compatibility excluded - new repository and one initial schema - cost if wrong: no existing-product upgrade guarantee.
- extreme stored versions/manually corrupted DB states excluded - immutable fixtures and one-way transition cannot create them - cost if wrong: unsupported states may return unavailable.
- review did not independently rerun integration - reviewer inspected source/safe evidence and ran in-memory probes while dependencies stopped; implementer reruns full checks/acceptance after fixes - cost if wrong: integration evidence is not independently reproduced.
- private raw OAuth/credential/realm/env/screenshots excluded from reviewer access - secret boundary; tracked files separately exact-value scanned and screenshot personally inspected - cost if wrong: private artifacts lack a second-person review.

## Final branch state

The new repository has no base branch to merge back into and no remote. The approved
local codex/local-demo branch is retained. Public product creation/push needs Dmitry's
explicit approval under portfolio-plan/RULES.md. No public push is inferred from
implementation approval. Demo containers/listeners are stopped, named evidence volumes
and ignored local runtime/private results are retained.
