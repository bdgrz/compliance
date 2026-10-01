# R1-07 Risk assessment, treatment, and acceptance backend slice

This slice implements the [M0-D10](decisions/m0-d10-risk-method.md) data shape
for risks recorded by [risk-draft-v1](risk-draft-v1.md). It is partial delivery
under [#199](https://github.com/bdgrz/compliance/issues/199).

## Contract

All routes sit under `/api/v1/tenants/{tenant_id}/programs/{program_id}` and
require `program.manage` for the program.

- `POST .../risk-method/versions` publishes the next immutable method version
  (`expected_version`, five `likelihood_scale` and five `impact_scale`
  descriptors, optional `appetite_threshold` 1–25). Kind is `qualitative_5x5`
  and the reassessment interval is `P1Y`. `GET .../risk-method` and
  `GET .../risk-method/versions/{version}` read it.
- `POST .../risks/{risk_id}/assessments` records an `inherent`, `target`, or
  `residual` assessment with `method_version`, `likelihood` and `impact` (1–5),
  and `rationale`. The score is derived. Target and residual require a current
  inherent assessment on the same method version. A residual assessment requires
  a non-`mitigate` treatment or an accepted control treatment assertion.
- `PUT .../risks/{risk_id}/treatment` chooses `mitigate | accept | transfer |
  avoid` with a rationale.
- `POST .../risks/{risk_id}/acceptances` records the caller's personal,
  time-bounded acceptance of the current residual assessment. It is HTTP-only.
  `approver_authority` is `compliance_lead` or `executive` and must be held
  through the `risk.accept.compliance_lead` or `risk.accept.executive` program
  grant. A compliance lead may not accept above appetite or while appetite is
  unset. `expires_at` must be after acceptance and within 12 months. The
  residual assessor may not accept the same risk.
- `GET .../risks/{risk_id}/evaluation` and `.../evaluation/history` return the
  current evaluation and its attributable history. Status (`unassessed`,
  `assessed`, `treatment_chosen`, `residual_assessed`, `acceptance_pending`,
  `accepted`, `reassessment_due`) and acceptance expiry are evaluated at read
  time. An expired acceptance or an assessment older than one year reports
  `reassessment_due`.

Writes use `expected_revision` optimistic concurrency. The Portia request ID
identifies each assessment and acceptance, so a replayed request doesn't append
twice. MCP exposes the four reads as read-only tools; there are no MCP writes.

## Risk governance (#467)

Each program keeps one `risk-governance` ledger holding every risk's owner,
control treatment assertions, and reassessment triggers. Each risk has its own
governance revision for `expected_revision`. Writes are HTTP-only;
`GET .../risks/{risk_id}/governance` is also a read-only MCP tool.

- `PUT .../risks/{risk_id}/owner` assigns a recorded workforce person, who need
  not sign in, as risk owner. The person's correlated member is snapshotted.
- `POST .../risks/{risk_id}/control-treatments` asserts that the exact current
  approved version of a same-program control treats a risk whose treatment is
  `mitigate`. `POST .../control-treatments/{treatment_id}/reviews` accepts or
  rejects it; the proposer may review only under a waiver scoped to
  `risk_control_treatment`. Accepting a later control version supersedes the
  earlier assertion for that control. `.../retirements` withdraws an accepted
  assertion. A residual assessment under `mitigate` requires an accepted
  assertion; an assertion never states that the control operates.
- The risk owner's correlated member (at assignment or now) may not accept the
  risk without an approved waiver scoped to (`risk`, risk ID, residual
  assessment ID, expected revision, `approve`); the acceptance records the
  waiver.
- Reactors raise durable `RiskReassessmentTrigger` records (`trigger_kind`,
  `source_reference`, `raised_at`): `method_changed` when a later method version
  is published (risks already assessed on it are skipped) and
  `boundary_changed` when a successor boundary version is approved. Trigger
  identity is stable, so replays append nothing. A trigger is open until an
  inherent assessment is recorded at or after it; an open trigger makes the
  evaluation status `reassessment_due`.

## Deferred

- Vendor and incident reassessment triggers, which need their owning sources.
- Program-level evaluation lists and readiness contribution; readiness rule
  definitions belong to R1-08.
