# Control operation v1: operating plans, occurrences, and findings

Backend contract for [R2-01 #270](https://github.com/bdgrz/compliance/issues/270),
[R2-04 #278](https://github.com/bdgrz/compliance/issues/278), and
[R2-07 #274](https://github.com/bdgrz/compliance/issues/274). It applies M0-D03,
M0-D13, M0-D15, M0-D19, and M0-D23. It does not change readiness rules (R1-08).

## Records and streams

| Record | Stream | Concurrency |
| --- | --- | --- |
| `ControlOperatingPlan` versions and `ControlOccurrence` with versioned `ControlAttestation` and review decisions | `control-operations/{program_id}` | Per control plan line (`expected_revision`) and per occurrence |
| `Finding` with `CorrectiveAction`, linked acceptances, verification, and closure decision | `remediation/{program_id}` | Per finding |

Reads hydrate these program streams, so they never lag. The blocker list also
reads the control directory projection to find active controls. A control that
the ledger already plans is always evaluated, even when the directory lags.

## R2-01 operating plan

- A plan targets the control's exact current approved version. It names an
  owner, an optional backup owner, a reviewer member, a cadence, and an effective
  date. Expected evidence is copied from the control version.
- Holders are a `member`, a workforce `person` who may never sign in, or a
  `team`. A team holds the work, but it is never the performer of record.
- Cadence is `recurring` (weekly, monthly, quarterly, semiannual, or annual,
  with a first period start and due days), `event_driven` (with a trigger), or
  `ad_hoc`. The response describes it in user language.
- A Compliance Lead or Org Admin (`program.manage`) proposes a plan. A different
  manager approves it, through HTTP only. Self-approval needs a waiver scoped to
  `(control_operating_plan, control_id, plan_version_id, plan revision, approve)`.
  A reviewer who also holds the work needs a waiver scoped to
  `(control_operating_plan, control_id, control_version_id, next revision, review)`.
- Approval supersedes the prior plan and reassigns open occurrences. Each
  reassignment is recorded explicitly. Historical performers never change.
- The blockers list reports `missing_plan`, `pending_approval`,
  `plan_for_superseded_version`, `owner_inactive`, `backup_owner_inactive`,
  `reviewer_inactive`, and `missed_occurrence`. `my-work` shows the actor's
  responsibilities, held directly or through a team. It also shows their due
  occurrences, pending reviews, and corrective actions, in M0-D15 order.
- Retirement or a successor ends a version's effective interval. That stops
  expected occurrences, and recorded ones stay readable.

## R2-04 performance and attestation

- Expected occurrences come from the approved cadence. An occurrence's ID is
  derived from the control and the period start. Event-driven and ad hoc
  occurrences are opened with a trigger.
- The owner or backup owner, directly or through a team, attests personally,
  through HTTP only. A holder or a manager may record a workforce person's
  off-product performance; the performer and the recorder are both attributed.
- Results are `complete`, `failed`, `not_applicable`, or `skipped`. A complete
  result needs support for every expected evidence item. Any other result needs a
  rationale.
- **Evidence references stay `unresolved`.** Governed evidence capture (#272,
  #196) waits on Portia S3 artifact storage. References keep their exact text so
  they can be resolved later. Until then, nothing proves the referenced content.
- A correction is a new attestation version that supersedes the prior one and
  records a reason. Prior versions and reviews are retained.
- The independent review outcome is `approved`, `returned`, `action_requested`,
  or `deferred`. Only the plan's reviewer or a manager may review, never the
  performer or recorder unless a waiver is scoped to
  `(control_occurrence, occurrence_id, attestation_id, attestation version, review)`.

## R2-07 findings

- A finding keeps its source kind, record, version, and verbatim source text.
  Governed sources (`readiness_gap`, `control_occurrence`) must exist in the
  program.
- The `ControlOccurrenceFindingsV1` reactor raises a finding for every failed or
  skipped attestation. It also raises a finding with a corrective action for
  every action a reviewer requests (M0-D19). Finding IDs are derived from the
  source, so replay is idempotent.
- Severity, owner, due date, scope, and root-cause changes require a reason and
  are recorded in history.
- An R1-07 risk acceptance or an approved waiver can be linked to a finding. It
  shows as `accepted` until it expires, then the finding is actionable again. It
  never closes the finding.
- Closure requires that all corrective actions are complete, plus resolution
  evidence and a verification rationale. A manager who is not the finding owner
  and did not own or complete an action decides it, through HTTP only. Otherwise
  a waiver scoped to `(finding, finding_id, finding_id, revision, approve)` is
  needed. Reopening keeps the closure in history.
- The readiness status is `closed`, `remediated`, `accepted`, `overdue`, or
  `unresolved`.

## Product defaults awaiting confirmation

Reactor-raised findings default to `medium` severity and are due in 30 days. They
are owned by the control owner (or the reviewer or recorder when the owner is not
a member). A Compliance Lead revises them. These defaults are not an M0 decision.
