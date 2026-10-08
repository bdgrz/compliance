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
| Risk governance | Treatment actions, completion reviews, control-treatment reviews | `risk-governance` | `FitzRiskGovernanceWorkItemDirectoryTests`, `RiskControlTreatmentWorkTests`, `WorkAssignmentTests` |
| Control evaluations | Submitted evaluation-round reviews | `control-evaluations` | `FitzControlEvaluationWorkItemDirectoryTests`, `ControlEvaluationWorkTests` |
| Criterion applicability | Not-applicable proposal reviews | `criterion-applicability` | `FitzCriterionApplicabilityWorkItemDirectoryTests`, `ControlMappingWorkTests` |
| Control mappings | Exact mapping-version reviews | `control-criterion-mappings` | `FitzControlMappingWorkItemDirectoryTests`, `ControlMappingWorkTests` |
| Operating plans | Pending operating-plan approvals | `control-operations` | `FitzControlOperatingPlanWorkItemDirectoryTests`, `ControlOperatingPlanWorkTests` |
| Policies | Draft and retirement reviews/approvals, periodic review | `policies` | `FitzPolicyDecisionWorkItemDirectoryTests`, `PolicyDecisionWorkTests` |
| Boundaries | Exact-revision assigned reviews/approvals | `boundaries` | `FitzBoundaryDecisionWorkItemDirectoryTests`, `WorkQueueTests` |
| Commitments | Exact-revision assigned reviews/approvals | `commitment-drafts` | `FitzCommitmentDecisionWorkItemDirectoryTests`, `CommitmentDecisionWorkTests` |
| Controls | Draft and retirement reviews/approvals | `controls` | `FitzControlDecisionWorkItemDirectoryTests`, `ControlDecisionWorkTests` |
| Policy campaigns | Acknowledgements and training completion | `policy-distribution-campaigns` | `FitzPolicyCampaignWorkItemDirectoryTests`, `PolicyCampaignWorkTests`, `TrainingWorkAuthorityTests` |
| Risk acceptance | Current residual-assessment acceptance | `risk-evaluations` | `RiskAcceptanceWorkTests` |

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

## Remaining acceptance boundaries

- Access-review campaigns remain tenant-scoped. Campaign placement,
  reviewer/remediation ownership and no-eligible-owner behavior require the
  decision tracked on #646 before that work can enter this program queue.
- Access expectations and pending SoD-waiver approval records are tenant-owned
  decisions without an assigned owning program. Their existence alone cannot
  authorize inferred program placement. They remain in their direct workflows;
  the source-placement decision must identify any additional consuming queue.
- Campaign waiver approval is an immediate personal decision without a pending
  request stage; provider coverage closure is a tenant-owned provider operation.
  Neither supplies a pending program reviewer assignment to copy into the queue.
- Queue-wide ADR 0007 acceptance remains under #284 until all required source
  coverage and reconciliation are established. Per-source checkpoint fencing
  does not promise cross-stream atomicity.
- Weekly email scheduling and delivery policy remain under #647 and their
  consuming issues. Search and in-app attention do not depend on email delivery.
- Client and end-to-end acceptance remain in the owning repositories and later
  validation phase; this backend inventory does not claim that acceptance.
