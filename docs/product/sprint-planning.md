# Sprint planning and forecasting

Planning baseline: 2026-10-11, [#940](https://github.com/bdgrz/compliance/issues/940).
[Project 2](https://github.com/orgs/bdgrz/projects/2) and issue acceptance are the
live record. The [delivery cycle](delivery-cycle.md) defines execution and stop
rules; [legacy Project 1](https://github.com/orgs/bdgrz/projects/1) preserves
broader acceptance and dependencies.

## Calendar and pilot

Sprints last 14 days, Wednesday through the following second Tuesday. Preserve
existing dates and accepted history: Sprint 1 October 14–27, Sprint 2 October
28–November 10, Sprint 3 November 11–24, and Sprint 4 November 25–December 8,
2026. Acceptance early on October 10–11 does not move those dates or create two
completed calendar samples.

The next selected sprint is a pilot of three shippable outcomes plus one ready
stretch outcome. This is a scope selection, not a demonstrated capacity promise.
Keep roughly 20% of planning capacity unallocated for findings and rework; do
not turn that allowance into measured spare hours. Work one bounded task at a
time, with architect → coder → reviewer sequentially and one clean checkout per
repository. Promote stretch only after all three original outcomes are accepted.
At review, explicitly accept, re-scope, return to refinement or reselect remaining
work; do not carry it into another sprint automatically.

## Sprint 3: review, publish and describe scope

| Outcome | Commitment | Readiness | Size / Mix | First bounded task and limit |
| --- | --- | --- | --- | --- |
| [#915 Independent control review](https://github.com/bdgrz/compliance/issues/915) | Committed | Ready | M / Mixed | Prove or add a manual handoff of the exact current draft revision to an eligible reviewer, using existing personal HTTP review outcomes. No notification, persisted request status or new request workflow. |
| [#916 Publish/read approved control](https://github.com/bdgrz/compliance/issues/916) | Committed | Sequenced after #915 | M / Mixed | Compose current Person ownership, accepted exact review and personal HTTP first approval with immutable/effective read evidence. Retain authority, stale-source refusals and default-off activation; no successor or retirement work. |
| [#927 Save/reopen boundary draft](https://github.com/bdgrz/compliance/issues/927) | Committed | Ready | S / Verification | Prove successful existing client save → leave/reopen → retained structured draft/revision. Reuse backend owner/rationale/category, unresolved-reference and denial evidence; do not rebuild the boundary model. |
| [#941 Select/reopen program criteria catalog](https://github.com/bdgrz/compliance/issues/941) | Stretch | Ready | S / Verification | Prove current selection → revision refresh → reopen retained edition, provenance/support limits and criteria. No mappings, catalog authoring or licensed text. |

A review request in #915 is a manual handoff/share of the exact draft revision;
reviewers record existing `accept` or `request_changes` outcomes through personal
HTTP. A minimal copy-link affordance, if missing, is its own bounded client task.
The accepted review must match current content. #916 follows #915 and preserves
existing ownership, independence and effective-period rules. Ready stories still
need an exact allowed-change brief before any task becomes In Progress.

The first-task checks reuse inspected APIs/UI/tests. They are not claims that the
whole stories are already accepted. Existing boundary/catalog client tests prove
payloads and feedback but lack the successful reopen proof selected here. A real
implementation defect stops an evidence task for re-scoping; it does not justify
following a dependency chain. Backend/client PRs stay in their owning repositories.

## Sprint 4 and later horizons

Sprint 4 is a conditional forecast, November 25–December 8:

| Outcome | Current planning state | Condition before selection |
| --- | --- | --- |
| [#928 First application/system instance](https://github.com/bdgrz/compliance/issues/928) | Forecast; Needs refinement; L - split; Unclassified | Split manual Application ownership/readback from canonical SystemInstance identity. Reconcile accepted [M0-D05](decisions/m0-d05-application-inventory.md) platform/provider uniqueness and required fields with current contracts before commitment. Preserve acceptance. |
| [#942 Map an approved control to a criterion](https://github.com/bdgrz/compliance/issues/942) | Forecast; Needs refinement; Unestimated | Bound one mapping workflow after accepted publication and catalog selection, retaining authority and exact-version attribution. |
| [#943 Record/reopen a manual provider](https://github.com/bdgrz/compliance/issues/943) | Forecast; Needs refinement; Unestimated | Select one manual provider record/readback slice from existing contracts; separate assurance, recurring review and imports. |

If #941 remains unshipped, select it before mapping and explicitly defer one
forecast. #928 is not ready stretch: the current name/kind/access-boundary form
and optional source identifier do not satisfy the decided platform-qualified
identity contract. A larger parent needs independently shippable splits before
it can enter a commitment.

Only these two upcoming sprints have near-term scope. Later horizons remain
unscheduled until decomposed into comparable user outcomes with acceptance:

| Horizon | Scope retained and planning boundary |
| --- | --- |
| [#900 Workspace](https://github.com/bdgrz/compliance/issues/900) | Accepted #910–#913 scope only; history does not close broader legacy organization acceptance. |
| [#901 Accountable controls](https://github.com/bdgrz/compliance/issues/901) / [#902 Program scope](https://github.com/bdgrz/compliance/issues/902) | Review/publication, mapping and manual record families need individual slices; boundary drafts do not imply complete inventories or approved scope. |
| [#903 Policy/risk](https://github.com/bdgrz/compliance/issues/903) / [#904 Work/evidence](https://github.com/bdgrz/compliance/issues/904) | Decompose policy/risk decisions, recurring work and retained evidence before dates. Public storage/inspection/upload gaps [#196](https://github.com/bdgrz/compliance/issues/196) and [#272](https://github.com/bdgrz/compliance/issues/272) remain to be delivered. |
| [#905 Readiness](https://github.com/bdgrz/compliance/issues/905) / [#906 Examination evidence](https://github.com/bdgrz/compliance/issues/906) | Separate management readiness/gap plans from examination requests, delivery and auditor conclusions. |
| [#907 Professional teams](https://github.com/bdgrz/compliance/issues/907) / [#908 Imports](https://github.com/bdgrz/compliance/issues/908) / [#909 Organization operations](https://github.com/bdgrz/compliance/issues/909) | Keep engagement/independence, optional import automation, and commercial/billing/operations work separately scoped; none silently blocks the manual application path. |
| [#636 Platform foundations](https://github.com/bdgrz/compliance/issues/636) / [#637 Decisions and facts](https://github.com/bdgrz/compliance/issues/637) | Track cross-cutting enablers and named fact discovery separately. A dependency gates only its recorded consumer. |

Native clients, broad E2E/browser validation and release/deployment qualification
require separate scope and evidence. Backend/web acceptance does not imply those
horizons are covered. The pre-planning snapshot had 338 open issues, including
155 E2E issues; those counts mix stories, tasks, legacy scope and validation,
so they are not a count of remaining features.

## Fields and sizing

| Field | Values and interpretation |
| --- | --- |
| Level | Epic, Story, Task. Count accepted Stories as user-outcome throughput; keep task/PR counts as context. |
| Commitment | Committed, Stretch, Forecast, Unscheduled. Forecast dates are conditional; stretch is never an extra commitment. |
| Readiness | Ready, Sequenced, Needs refinement, Blocked, Accepted. Sequenced names an unfinished predecessor; Blocked names the exact missing input. |
| Size | S, M, L - split, Unestimated. S is one bounded task/evidence gap; M needs several bounded tasks or coordinated acceptance; L must split. Sizes are provisional relative comparisons, with no hour conversion. |
| Mix | Implementation, Verification, Mixed, Discovery, Unclassified. Describe remaining work; retain Unclassified when the mix is not yet known. |

Order selects the next eligible task under WIP = 1. A task needs a 20-minute-capped
readiness brief with observable gap, owner/repository, allowed change, exclusions,
direct dependencies, focused check and stopping point. An additional workflow,
repository, unplanned contract or unknown scope stops activation until re-scoped.
Do not add tasks just to increase apparent throughput.

## Measurement and forecast method

The early evidence records wall-clock elapsed time, not active engineering hours:

| Accepted story | Recorded activation → acceptance elapsed | Limitation |
| --- | --- | --- |
| #910 Login | 128.46 minutes | Work/review/rework intervals were not measured separately. |
| #911 Tenant setup | 136.38 minutes | Work/review/rework intervals were not measured separately. |
| #912 First control | Unknown | Activation time was not recorded. |
| #913 Invitation | 148 minutes 14 seconds | Includes a pause and intervening #914 work. |
| #914 Accountable owner | 60 minutes 52 seconds | Wall time is not a measured active-hours value. |

All five were accepted October 10–11, before the planned Sprint 1/2 starts.
They may inform comparable task mix and elapsed-cycle observations; they cannot
establish completed outcomes per actual 14-day sprint. Unknown intervals remain
unknown rather than zero.

For each activated story, record this small measurement receipt in its issue:

```text
Story / size / remaining mix:
Activation UTC / acceptance UTC:
Measured work intervals / active time (or unknown):
Blocked time / review wait / rework time (each measured or unknown):
Owning repos / task and PR counts (context only):
Acceptance evidence / scope changes:
Carryover decision and next action:
```

At each actual calendar close, reconcile accepted original commitments, any
promoted stretch, carryover reasons, scope changes, mix and elapsed/active-time
data. Recalibrate the next selection. After 2–3 comparable calendar cycles,
forecast only a refined tranche using observed low/typical/high accepted
outcomes per 14 days. State the tranche, definition of Done, mix, availability,
dependencies and deferred validation/deployment assumptions; report a range.
Recompute when those assumptions change. Do not divide the raw backlog by a PR
rate or convert provisional sizes to hours. There is no evidence-backed whole
project completion date yet.

Jev `jev-1.13.0` advised the manual review handoff, bounded pilot and separate rolling
horizons during this planning pass. Its judgments challenge the plan; accepted
decisions, inspected contracts/tests, live issue state and reviewed evidence
remain authoritative.
