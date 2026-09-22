# M0-D10: Risk assessment method and acceptance authority

Status: decided, 2026-09-22. Decision owner: Jeff Repanich, product owner.
The first client's appetite value and advisor confirmation are in
[#355](https://github.com/bdgrz/compliance/issues/355).

| Question | Decision and rationale |
| --- | --- |
| Method and scales | Qualitative 5×5. Likelihood 1–5 and impact 1–5; score = likelihood × impact (1–25). Both inherent and residual assessments are recorded. This is simple enough for a small team and familiar to auditors. |
| Materiality, appetite, tolerance | Appetite is a per-program residual-score threshold (1–25) recorded on the method version. There is no invented default; the first client's value is set in #355. A residual score above appetite is "above appetite". |
| Treatment and acceptance | Treatments: `mitigate`, `accept`, `transfer`, `avoid`. Acceptance is time-bounded: the Compliance Lead may accept a risk at or below appetite for up to 12 months. Accepting a risk above appetite requires an executive approver. Acceptance expires and returns the risk to reassessment. |
| Cadence and triggers | Full reassessment annually. Trigger events that flag a risk for reassessment: significant change or incident (M0-D21), boundary change, new material vendor, and an expired acceptance. |
| Consultant confirmation | Confirming the method with the readiness advisor is part of #355. |

## Data-shape rules

- `RiskMethod`: versioned per program. Fields: `kind` (`qualitative_5x5`), `likelihood_scale[5]` and `impact_scale[5]` descriptors, `appetite_threshold` (nullable until set), and `reassessment_interval` (`P1Y`).
- `RiskAssessment`: `risk_id`, `method_version_id`, `phase` (`inherent | residual`), `likelihood` (1..5), `impact` (1..5), `score` (derived, never user-entered), `assessor_member_id`, and `assessed_at`. A residual assessment requires at least one `ControlRiskTreatment` or a non-mitigate treatment.
- `RiskTreatment`: `risk_id`, `kind` ∈ `mitigate | accept | transfer | avoid`, and `rationale`.
- `RiskAcceptance`: `risk_id`, `residual_assessment_id`, `approver_member_id`, `approver_authority` (`compliance_lead | executive`), `accepted_at`, and `expires_at` (≤ `accepted_at` + 12 months). It is invalid when the residual score exceeds appetite and the authority is `compliance_lead`, or when the approver is the risk owner without an SoD exception.
- `RiskReassessmentTrigger`: `risk_id`, `trigger_kind`, `source_reference`, and `raised_at`.

## Canonical model alignment

This extends the existing `ControlRiskTreatment` and `IncidentReference`. The
`RiskMethod`, `RiskAssessment`, and `RiskAcceptance` fields are alignments
requested on M0-D28. The existing risk draft
([risk-draft-v1](../risk-draft-v1.md)) remains `draft_unassessed` until these
records exist.
