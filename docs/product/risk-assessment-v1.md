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
  a non-`mitigate` treatment until control-to-risk treatments exist.
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

## Deferred

- Control-version-to-risk-treatment assertions (#197) and residual assessment
  under `mitigate`.
- Reassessment trigger reactors for boundary, vendor, incident, and method
  change events, and a durable `RiskReassessmentTrigger` record.
- Owner-based separation of duties and SoD waivers, since risk owner resolution
  is still open.
- Program-level evaluation lists and readiness contribution.
