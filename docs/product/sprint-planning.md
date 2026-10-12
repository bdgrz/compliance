# Sprint planning and forecasting

Historical planning baseline: 2026-10-11, [#940](https://github.com/bdgrz/compliance/issues/940).
Accepted scope reconciled: 2026-10-12, [#972](https://github.com/bdgrz/compliance/issues/972).
[Project 2](https://github.com/orgs/bdgrz/projects/2) and issue acceptance are the
live record. The [delivery cycle](delivery-cycle.md) defines execution and stop
rules; [legacy Project 1](https://github.com/orgs/bdgrz/projects/1) preserves
broader acceptance and dependencies.

## Calendar and accepted scope

Sprints last 14 days, Wednesday through the following second Tuesday. The live
Project 2 readback on October 12 records all 15 Stories assigned to Sprints 1–6
as Done / Accepted, before their assigned iteration dates. Preserve those dates
and assignments as accepted-early history:

| Sprint | Preserved dates | Accepted Stories |
| --- | --- | --- |
| 1 — Shared workspace | October 14–27, 2026 | #910, #911, #912 |
| 2 — Collaborate | October 28–November 10, 2026 | #913, #914 |
| 3 — Review and publish | November 11–24, 2026 | #915, #916, #927, #941 |
| 4 — Describe scope | November 25–December 8, 2026 | #942, #943, #950 |
| 5 — Record applications | December 9–22, 2026 | web #45, backend #954 |
| 6 — Revise applications | December 23, 2026–January 5, 2027 | #955 |

Early acceptance does not move the calendar or provide a completed 14-day
velocity sample. These six assignments do not represent six elapsed sprints or
demonstrated sprint capacity. They select no new work.

All rows below are Done / Accepted. Size and mix retain the current recorded
planning fields; they are not measurements of effort.

| Sprint | Accepted outcome | Size / Mix |
| --- | --- | --- |
| 1 | [#910 Sign in and return to my app](https://github.com/bdgrz/compliance/issues/910) | Unestimated / Mixed |
| 1 | [#911 Create and reopen my organization](https://github.com/bdgrz/compliance/issues/911) | Unestimated / Mixed |
| 1 | [#912 Save and reopen my first control draft](https://github.com/bdgrz/compliance/issues/912) | Unestimated / Mixed |
| 2 | [#913 Invite a collaborator into my workspace](https://github.com/bdgrz/compliance/issues/913) | Unestimated / Mixed |
| 2 | [#914 Assign an accountable control owner](https://github.com/bdgrz/compliance/issues/914) | Unestimated / Mixed |
| 3 | [#915 Obtain an independent control review](https://github.com/bdgrz/compliance/issues/915) | M / Mixed |
| 3 | [#916 Publish and read my approved control](https://github.com/bdgrz/compliance/issues/916) | M / Mixed |
| 3 | [#927 Save and reopen my program boundary draft](https://github.com/bdgrz/compliance/issues/927) | S / Verification |
| 3 | [#941 Select and reopen my program criteria catalog](https://github.com/bdgrz/compliance/issues/941) | S / Mixed |
| 4 | [#942 Map my approved control to a criterion](https://github.com/bdgrz/compliance/issues/942) | S / Mixed |
| 4 | [#943 Record and reopen my first manual provider](https://github.com/bdgrz/compliance/issues/943) | S / Mixed |
| 4 | [#950 Save and reopen a populated manual Application](https://github.com/bdgrz/compliance/issues/950) | S / Verification |
| 5 | [web #45 Open organization Application inventory from a program](https://github.com/bdgrz/compliance-web/issues/45) | S / Implementation |
| 5 | [backend #954 Record and reopen a required-field-complete manual Application](https://github.com/bdgrz/compliance/issues/954) | M / Implementation |
| 6 | [#955 Revise required Application metadata without losing identity or work](https://github.com/bdgrz/compliance/issues/955) | M / Implementation |

The original #940 Sprint 3 pilot selected three commitments plus #941 as stretch;
all four are now accepted. That completed planning baseline remains historical.
Issue acceptance records define each delivered boundary: manual exact-revision
review does not imply a notification workflow; approved-control publication does
not imply successor or retirement workflows; boundary/catalog and manual-record
acceptance does not imply complete inventories, imports or licensed content.

## Remaining refinement and later horizons

There are no non-Done Ready Stories in the October 12 Project 2 readback. The next
planning action is bounded refinement of
[#928](https://github.com/bdgrz/compliance/issues/928), not a new commitment or
implementation. It remains **Unscheduled / Needs refinement / L - split /
Discovery**, with no sprint assignment, and is a non-throughput Epic/coordinator.

Cap that refinement at 20 minutes: reconcile accepted Application child evidence
with the remaining canonical SystemInstance identity/readback and metadata-edit
scope, then record one observable gap, owning repository, exact allowed change,
dependencies, focused check and stopping point before any implementation task.
Preserve #928's acceptance and the accepted
[M0-D05](decisions/m0-d05-application-inventory.md) identity and
[M0-D22](decisions/m0-d22-canonical-ownership.md) ownership contracts, including
required classification and accountable owners, platform-qualified identity and
uniqueness, attribution, explicit unresolved information, and tenant/stale-write
fences. Accepted Application children do not close the coordinator or the broader
legacy acceptance. Recording an inventory item does not approve boundary
membership or access-review scope.

[#952 Canonical SystemInstance recording](https://github.com/bdgrz/compliance/issues/952)
and [#953 Identity-preserving metadata revision](https://github.com/bdgrz/compliance/issues/953),
along with the other Needs-refinement Stories, remain unscheduled and unaccepted.
Do not weaken canonical acceptance to match an existing form or silently turn a
discovery into implementation. No new dates, estimates or capacity are selected
by this reconciliation.

Broader horizons remain unscheduled until decomposed into comparable user
outcomes with acceptance:

| Horizon | Scope retained and planning boundary |
| --- | --- |
| [#900 Workspace](https://github.com/bdgrz/compliance/issues/900) | Accepted #910–#913 scope only; history does not close broader legacy organization acceptance. |
| [#901 Accountable controls](https://github.com/bdgrz/compliance/issues/901) / [#902 Program scope](https://github.com/bdgrz/compliance/issues/902) | Accepted review/publication, catalog selection, mapping and manual-record Stories do not close remaining control, boundary or inventory acceptance. Boundary drafts do not imply approved scope. |
| [#903 Policy/risk](https://github.com/bdgrz/compliance/issues/903) / [#904 Work/evidence](https://github.com/bdgrz/compliance/issues/904) | Decompose policy/risk decisions, recurring work and retained evidence before dates. Public storage/inspection/upload gaps [#196](https://github.com/bdgrz/compliance/issues/196) and [#272](https://github.com/bdgrz/compliance/issues/272) remain to be delivered. |
| [#905 Readiness](https://github.com/bdgrz/compliance/issues/905) / [#906 Examination evidence](https://github.com/bdgrz/compliance/issues/906) | Separate management readiness/gap plans from examination requests, delivery and auditor conclusions. |
| [#907 Professional teams](https://github.com/bdgrz/compliance/issues/907) / [#908 Imports](https://github.com/bdgrz/compliance/issues/908) / [#909 Organization operations](https://github.com/bdgrz/compliance/issues/909) | Keep engagement/independence, optional import automation, and commercial/billing/operations work separately scoped; none silently blocks the manual application path. |
| [#636 Platform foundations](https://github.com/bdgrz/compliance/issues/636) / [#637 Decisions and facts](https://github.com/bdgrz/compliance/issues/637) | Track cross-cutting enablers and named fact discovery separately. A dependency gates only its recorded consumer. |

Native clients, broad E2E/browser validation and release/deployment qualification
require separate scope and evidence. Backend/web acceptance does not imply those
horizons are covered. Unresolved external inputs need named owners and directly
affected acceptance; they do not silently block unrelated manual workflows.
The pre-planning snapshot had 338 open issues, including
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

For a future selection, keep the existing roughly 20% planning allowance for
findings and rework; it is not measured spare hours or a capacity promise. Work
one bounded task at a time, with architect → coder → reviewer sequentially and
one clean checkout per repository. Promote stretch only after the original
commitments are accepted. At review, explicitly accept, re-scope, return to
refinement or reselect remaining work; do not carry it forward automatically.

## Measurement and forecast method

The existing #940 measurements remain unchanged. They record wall-clock elapsed
time, not active engineering hours:

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

This reconciliation adds no timing measurements for the other ten accepted
Stories. All 15 were accepted before their assigned dates; none supplies a
completed 14-day calendar velocity sample. Tasks, PRs and coordinator epics are
context, not additional accepted Stories. Deferred validation and unresolved
external inputs do not count as delivered outcomes.

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
horizons at the historical #940 baseline. Its judgments challenge the plan; accepted
decisions, inspected contracts/tests, live issue state and reviewed evidence
remain authoritative.
