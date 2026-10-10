# Work queue source coverage

The program queue joins source work with current membership, responsibility,
separation of duties and recorded accountability. Source workflows own completion.
This inventory records the implemented program source families; it does not
expand a tenant-owned workflow into a program queue or claim an immutable
cross-stream snapshot.

| Source family | Projected work | Source event area | Focused coverage |
| --- | --- | --- | --- |
| Control operations | Occurrences and independent occurrence reviews | `control-operations` | `FitzControlOccurrenceWorkItemDirectoryTests`, `WorkQueueTests` |
| Evidence requests | Open owner fulfilment | `evidence-requests` | `FitzEvidenceWorkItemDirectoryTests`, `WorkQueueTests` |
| Findings | Corrective actions and independent finding closure | `remediation` | `FitzCorrectiveActionWorkItemDirectoryTests`, `FitzFindingClosureWorkItemDirectoryTests`, `FindingClosureWorkTests` |
| Risk governance | Treatment actions, completion reviews, control-treatment reviews | `risk-governance` | `FitzRiskGovernanceWorkItemDirectoryTests`, `RiskControlTreatmentWorkTests`, `WorkAssignmentTests`, `RiskGovernanceHandlerTests` |
| Control evaluations | Submitted evaluation-round reviews | `control-evaluations` | `FitzControlEvaluationWorkItemDirectoryTests`, `ControlEvaluationWorkTests` |
| Criterion applicability | Not-applicable proposal reviews | `criterion-applicability` | `FitzCriterionApplicabilityWorkItemDirectoryTests`, `ControlMappingWorkTests` |
| Control mappings | Exact mapping-version reviews | `control-criterion-mappings` | `FitzControlMappingWorkItemDirectoryTests`, `ControlMappingWorkTests` |
| Operating plans | Pending operating-plan approvals | `control-operations` | `FitzControlOperatingPlanWorkItemDirectoryTests`, `ControlOperatingPlanWorkTests` |
| Policies | Draft and retirement reviews/approvals, periodic review | `policies` | `FitzPolicyDecisionWorkItemDirectoryTests`, `PolicyDecisionWorkTests` |
| Boundaries | Exact-revision assigned reviews/approvals | `boundaries` | `FitzBoundaryDecisionWorkItemDirectoryTests`, `WorkQueueTests` |
| Commitments | Exact-revision assigned reviews/approvals | `commitment-drafts` | `FitzCommitmentDecisionWorkItemDirectoryTests`, `CommitmentDecisionWorkTests` |
| Controls | Draft and retirement reviews/approvals | `controls` | `FitzControlDecisionWorkItemDirectoryTests`, `ControlDecisionWorkTests` |
| Policy campaigns | Acknowledgements and training completion | `policy-distribution-campaigns` | `FitzPolicyCampaignWorkItemDirectoryTests`, `PolicyCampaignWorkTests`, `TrainingWorkAuthorityTests` |
| Access-review campaigns | Frozen reviewer decisions and pending remediation verification | `access-review-campaigns` | `FitzAccessReviewCampaignWorkItemDirectoryTests`, `AccessReviewWorkQueueVisibilityTests`, `WorkSourceCompositionTests` |
| Risk acceptance | Current residual-assessment acceptance | `risk-evaluations` | `RiskAcceptanceWorkTests`, `RiskAcceptanceWorkAuthorityTests`, `RiskAcceptanceWorkCompositionTests` |

Each production source reader is registered as an accountable-work reader and
participates in the queue's capture and final confirmation of its own checkpoint.
Its source inputs and checkpoint commit together in the corresponding Fitz
projection. Risk acceptance reuses the existing risk-evaluation projection and
checks the current risk inputs. Time-based horizons and current responsibility
windows remain read-time rules. Source-specific tests cover lifecycle and stale
inputs; `WorkQueueReadConsistencyTests` covers the per-source queue fence.

`WorkQueueSearchTests` additionally joins real evidence, corrective-action and
finding-closure projections, reconciles source actions with item reads, and
proves visible scoped counts under search. A search with no visible match still
fails with a transient conflict while a required projection is behind.

`WorkSourceCompositionTests` exercises the production `AddCompliance` registration
with all 16 accountable readers. It replays retained source events through each
reader's real transactional store. The mixed evidence, access-review,
corrective-action and finding-closure scenario verifies initial item/detail/search
agreement. After campaign decisions and evidence cancellation, it verifies
per-source lag conflicts, updated lists and counts, and 404s for removed items.
A second scenario seeds every registered production kind
and reconciles list, detail, search, action metadata, and all four counts across
the resulting 30-item queue. It cancels a source, verifies a transient conflict
while projection is behind, then verifies catch-up removes the item from list and
detail and decrements the count. These mixed-source tests complement the
source-specific lifecycle, ordering, authorization, and projection-freshness
tests; per-source checkpoint fencing still does not imply a global atomic
snapshot.

The runtime registration audit resolves 16 distinct checkpoint identities and
30 current program work kinds, with each kind supplied once. Empty resources
load their own start checkpoints and each reader uses a tenant-specific source
pattern. The campaign reader uses only the Program and remediation owner frozen
in a routed launch; legacy tenant-direct campaigns remain unrouted and are not
placed by inference. The [#646 decision record](decisions/r2-access-review-campaign-routing.md)
defines this placement and the current eligibility-loss path.

Campaign Fitz rows retain only queue-required scope and status: tenant, Program,
campaign, item, system instance, due date, current reviewer/remediation owner,
decision kind, verification and exception state, provider-change presence, and
the member key needed to detect self-review. They do not duplicate frozen
population contents, access paths, identities, rationales, provider descriptions,
or verification evidence. A self-review candidate stays visible to authorized
oversight with an explicit exact-scope waiver signal; ordinary action eligibility
and queue assignment remain false, while the source command continues to enforce
M0-D07 and its existing waiver checks. An audited reviewer reassignment records
both why the prior owner lost eligibility and why the replacement qualifies as
a delegate.

## Search contract

`GET /api/v1/tenants/{tenant_id}/programs/{program_id}/work` and the existing
read-only `bdgrz.work.list` MCP operation accept optional `search`. The literal
term is trimmed, limited to 200 characters, and matched against visible summary,
reason or work kind with ordinal case-insensitive matching. Empty or whitespace
terms preserve the existing response. Search never grants visibility or matches
hidden source payloads.

The existing `scope` and `horizon_days` behavior is preserved. Counts describe
the returned scope's matching items: total, overdue, due today and escalated.
They are not independent tab totals. Filtering preserves the queue's existing
due date, materiality, creation time and ID ordering. Item lookup and source
commands retain their authorization and freshness checks.

## Campaign recording authority

Training completion work asks the campaign owner to record reviewed manual or
LMS-export evidence. The owner must currently hold `program.manage`; another
active program manager can receive delegated work. Policy acknowledgements for
people without a correlated platform member use the same current management
requirement for on-behalf recording. If the owner loses authority or membership,
recording work becomes unassigned and eligible managers can claim it. Explicitly
authorized guest memberships follow the source recording permission; firm staff
remain excluded. This recording authority does not widen independent reviewer
roles. Counts,
item lookup and assignment use the same source eligibility rule.

Correlated policy audience members still acknowledge personally. A deprovisioned
but still-correlated member leaves an explicit unassigned personal item; manager
routing never authorizes acknowledgement on that person's behalf. Source
commands retain their existing grants, audience and version checks. Existing
campaign projection records and work identities remain compatible.

## Management decision authority

Evaluation-round reviews, operating-plan approvals, control-mapping reviews,
criterion-applicability reviews, independent finding closure, policy decisions,
control decision fallback, commitment decision fallback and risk governance
reviews use `program_manager` responsibility. Their source commands accept
current active non-firm-staff members with explicit `program.manage`, including
persisted guest memberships. Queue eligibility,
assignment and scoped counts use that same authority. The shared source
candidate factories keep projected and fallback reads aligned without changing
work identity or persistence. Existing proposer/evaluator exclusions still
apply; decisions retain their source version and independence checks.

Named control decision duties also require that current non-firm management
grant. Named commitment and boundary duties keep their additional current source
management requirement. Other source roles keep their existing requirements.
The legacy `program_reviewer` holder itself remains client-personnel-only.
Finding ownership, corrective-action ownership and recorded completer exclusions still deny ordinary closure work regardless of management grant or
membership affiliation; the correction grants no closure exception or waiver.
Risk action owners and completion submitters remain excluded from independent
completion review, including explicitly granted guests. Grant loss invalidates
recorded assignments under the same eligibility rule used by counts and reads.

Policy decision work uses `AccountableWorkItemPolicyDecisionV2` and the separate
`policy-decisions-v2` Fitz resource. The worker replays retained policy events
into this generation's own rows, numeric revision and transactional checkpoint.
V1 rows, revision and checkpoint remain unchanged for audit. Live queue readers
use V2 only and return a transient conflict until its policy source cursor is
caught up, even when V1 is current. V2 rows require the exact owning program's
`program_manager` holder; legacy, unknown and wrong-program holders fail closed.
API and worker must run the matching V2 implementation for catch-up to complete.
This is the ADR 0007 generation rule for this interpretation correction. The
all-kind queue reconciliation evidence is recorded in PR #889; each source still
uses its own transactional checkpoint, without a global atomic snapshot.

## Risk completion assignment identity

The completion-review source handler reads the same program/risk/submission
work identity emitted by both the authoritative fallback and Fitz projection.
Assignments created through the queue authorize only that exact pending
submission. Reassignment removes the previous reviewer's authority; a rejected
completion followed by resubmission requires a new assignment. Source revision,
evidence, ownership and independence checks still apply.

The program/risk namespace introduced in #616 is retained. Historical assignment
entries using the earlier submission-only identity remain audit records and
do not authorize current namespaced work. A pending review needs an assignment
through its current queue item; no compatibility fallback reads the old key.

## Risk acceptance authority

Risk acceptance requires both current `program.manage` and the applicable risk
acceptance grant. Active non-firm-staff members, including explicitly authorized
persisted guests, follow the source command's authority. An executive grant is
required above appetite or when no appetite threshold is published. Current
residual assessors and risk owners remain excluded from ordinary queue action;
the source's approved owner-waiver path stays in its direct workflow.

Projected and fallback candidates apply the same requirements. Grant loss removes
assigned action visibility and counts immediately and leaves the pending item
unassigned for eligible replacement. This correction preserves acceptance work
identity, recorded responsibility, source revisions and expiry rules. Production
composition tests verify the registered queue and acceptance authorization
boundaries. An allowed acceptance also executes the production-registered
handler and records its returned acceptance in the retained source aggregate.

## Remaining acceptance boundaries

- `ComplianceProgram` currently exposes creation but no inactive or archived
  lifecycle state. Campaign launch therefore requires an existing Program in
  the active tenant and current Program-management authorization, matching the
  available source contract. Whether a created Program can become inactive is
  unresolved; this slice does not invent lifecycle state. The [#646 decision
  record](decisions/r2-access-review-campaign-routing.md) captures this limit.
- Access expectations and pending SoD-waiver approval records are tenant-owned
  decisions without an assigned owning program. Their existence alone cannot
  authorize inferred program placement. They remain in their direct workflows;
  the source-placement decision must identify any additional consuming queue.
- Campaign waiver approval is an immediate personal decision without a pending
  request stage; provider coverage closure is a tenant-owned provider operation.
  Neither supplies a pending program reviewer assignment to copy into the queue.
- Per-source checkpoint fencing does not promise cross-stream atomicity. A queue
  read captures and confirms each required source checkpoint around enumeration;
  ordinary writes after the final confirmation can still race the response.
- Weekly email scheduling and delivery policy remain under #647 and their
  consuming issues. Search and in-app attention do not depend on email delivery.
- Client and end-to-end acceptance remain in the owning repositories and later
  validation phase; this backend inventory does not claim that acceptance.

Boundary review and approval require both the current named operating duty and the current source `program.manage` grant. A duty assignment alone does not authorize the source decision. Revoked grants remove action eligibility immediately while preserving responsibility history and work identity.

### Historical management authority

Queue action eligibility and management assignment use the source request's `IClientManagementMutationRequest` metadata. The explicit source-kind/request map covers every registered production kind, including currently unmarked source commands, so a later concrete source marker also changes queue eligibility. Current membership, ordinary grants, named duty and source separation of duties remain required. Canonical user identity comes from the actual owning-client member; retained actual Attest history denies marked management work even after assignment closure or current client/guest relinking.

A recorded assignee who loses source eligibility becomes effectively unassigned without rewriting the retained assignment history. Mine and eligible-unassigned counts, claim, delegation, escalation and management assignment follow that source eligibility. Authorized program-manager and current-team read oversight retain the existing All/Team/detail visibility; a `next_action` describes the source workflow and grants no authority, as for existing separation-of-duties exclusions. Eligible replacement members can still claim or receive the orphaned work.

Personal policy acknowledgement remains unmarked and usable by its correlated
audience member. Its existing direct-member assignment and team-only claim
validation remain unchanged. Conditional manager proxy acknowledgement has a
shared request with personal acknowledgement and requires a separately accepted
source-aware guard; this metadata slice does not invent a denial before that
source contract exists. The [#646 placement rule](decisions/r2-access-review-campaign-routing.md)
is delivered through the campaign projection in [PR #884](https://github.com/bdgrz/compliance/pull/884).
PR #889 reconciles every registered source kind through the queue's list, detail,
search, action metadata, and count views. Source projection fences and retained
source history remain independent checks; this is not a cross-stream atomic
snapshot or authorization/write guarantee.

Conditional policy proxy recording uses `PolicyAcknowledgementRecorderGuard.EvaluateCapturedAsync` with the exact
runtime Person snapshot captured while deriving audience membership. The projected reader and retained-source fallback
capture that Person once per participant; neither stores that capture as projection authority nor rehydrates the
correlation when evaluating an actor. Actual Attest history denies proxy eligibility and manager assignment, orphaning
an existing assignment while preserving ordinary All/Team oversight. Direct member-held personal acknowledgement
remains available under its ordinary read/audience checks and retains the existing team-only Claim validation.
The whole `AcknowledgePolicy` DTO remains unmarked because its personal and proxy branches have different authority.

Current active team membership also supplies ordinary Team/All read oversight when the actor holds the existing
program source-read grant, even if retained Attest history removes source action eligibility. That visibility does
not supply an effective assignee, actionable Mine/unassigned work, claim, assignment, or delegation authority.
Removed, suspended and deprovisioned team members remain absent; existing restricted-source visibility fences apply.
