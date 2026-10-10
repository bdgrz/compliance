# R2 decision: Access-review campaign placement (#646)

Status: selected product default, 2026-10-10. Decision owner: product owner.

## Decision source and status

At the product owner's direction to use Jev API answers for the open questions
in [#646](https://github.com/bdgrz/compliance/issues/646), Jev-1.13.0 selected
the proposed campaign-placement rule below, with probability 1.0 and confidence
1.0. This record captures the product owner's selection; the model score is a
review signal, not decision authority. The earlier GitHub comment is explicitly
marked proposed and not approved. This record does not claim that campaign
routing has been implemented or that #646 or #284 is complete. #646 remains
open for issue readback, and implementation remains pending under #284.

## Selected placement rule

- Each new campaign launched into the accountable program queue names exactly
  one owner Program. At launch, the Program must be active and belong to the
  same tenant as the campaign. The launching actor must have current membership
  and the program and access-review authority required by the source workflow.
- The Program is an explicit queue placement. Do not infer it from the
  population, application, system instance, access owner, reviewer, or launch
  actor, and do not duplicate a campaign across unrelated programs or tenants.
- Freeze the accepted population identities and snapshots, resulting review
  items, named reviewers and assignments, instructions, deadline, and owner
  Program at launch. Later population or boundary changes do not rewrite or
  re-place launched work. Later observations are separate evidence or belong to
  a future campaign.
- Reviewer work belongs to the frozen named reviewer only while that person
  has active tenant membership, current permission to read the program queue,
  and visibility of the restricted system. Assignment supplies no source
  command authority. M0-D07 remains the reviewer rule: the system access owner
  or a delegate reviews, and a reviewer does not decide their own access except
  through the existing exact-scope separation-of-duties waiver path. Existing
  bulk-decision restrictions continue to apply.
- Each routed campaign also names an active, currently authorized remediation
  owner with `access_review.manage`. That owner must be distinct from the
  frozen reviewer. Existing source-command authorization remains in force for
  recording and verifying remediation. Verification still requires a later
  accepted population for the same system that shows the entitlement removed
  or changed; a ticket alone is not verification.
- Reject a new routed launch when its owner Program is missing, inactive, or
  outside the tenant, or when no eligible reviewer or distinct remediation
  owner can be named. Do not omit those responsibilities or guess a replacement
  queue.
- If the Program, reviewer, or remediation owner later loses eligibility,
  expose the affected pending work as orphaned. Preserve the launch and
  responsibility history, and require an attributable reassignment to an
  eligible person. Do not silently reroute the campaign or rewrite its frozen
  launch content.
- A legacy campaign with no recorded owner Program remains in the tenant-direct
  workflow and carries explicit unrouted follow-up. Do not infer or backfill a
  Program from later changes. Its source commands continue to enforce their
  existing authorization.

## Alignment and scope

M0-D07 already fixes the review population as an immutable accepted snapshot,
names the system access owner or delegate as reviewer, prohibits self-review by
default, and requires later population evidence to verify remediation. M0-D03
continues to govern separation-of-duties waivers. M0-D15 makes the queue a
projection of source work: queue visibility, assignment, or routing does not
grant permission to decide access or record remediation. Current source
authorization and restricted-system visibility apply on every read and action.

This resolves only the [#284 acceptance clause](https://github.com/bdgrz/compliance/issues/284):
“Access-review campaigns remain tenant-scoped. Campaign placement,
reviewer/remediation ownership and no-eligible-owner behavior require the
decision tracked on #646 before that work can enter this program queue.” It does
not decide placement for access expectations, pending separation-of-duties
approvals, provider coverage, or other tenant-owned sources. It does not resolve
[#647](https://github.com/bdgrz/compliance/issues/647), queue-wide source
coverage, count/action reconciliation, freshness acceptance, or any other
#284 criterion.

At this decision's worktree head, the source-coverage inventory still excludes
access-review campaign routing from the program queue. This record establishes
the product rule only; queue delivery and its acceptance evidence remain
pending. No API or persistence contract is established here.
