# M0-D11: Vendor materiality, due diligence, and subservice treatment

Status: decided, 2026-09-22. Decision owner: Jeff Repanich, product owner.
Classifying the first client's actual vendors is
[#356](https://github.com/bdgrz/compliance/issues/356).

| Question | Decision and rationale |
| --- | --- |
| Material-provider threshold | A provider is material when it stores or processes customer data, or it is on the critical path for an in-scope service. Spend does not count. This test is about control exposure, not cost. |
| Due-diligence set and cadence | A material provider needs a current SOC 2 report or ISO/IEC 27001 certificate. When neither exists, a completed security questionnaire is accepted. Reviewed on onboarding and annually. A non-material provider needs only a recorded classification rationale. |
| Assurance-report fields | Report kind, period start and end (or point-in-time date), the issuing firm, opinion (`unqualified | qualified | adverse | disclaimer`), covered services, noted exceptions, CUECs relevant to us, coverage gaps (services or period not covered), and bridge letter (period and date) when the report period ends before our need. |
| Subservice treatment | Subservice organizations (for example the cloud host) default to **carve-out**, and their CSOCs are recorded as `subservice_responsibility` commitments (M0-D09). Inclusive treatment is an explicit, reasoned exception. |

## Data-shape rules

- `Provider`: `provider_kind`, `materiality` (`material | not_material`), `materiality_basis[]` ⊆ `customer_data | critical_path`, `materiality_rationale`, `subservice` (bool), `boundary_treatment` (`carve_out | inclusive`, required when `subservice`), and an owner.
- `ProviderReview`: `provider_id`, `reviewed_at`, `next_review_due` (≤ `reviewed_at` + 1 year for material providers), `evidence_kind` (`soc2_type1 | soc2_type2 | iso27001 | questionnaire`), a `assurance_report_id` (nullable), `conclusion`, and a reviewer.
- `AssuranceReport`: the fields listed above, plus a governed artifact reference (M0-A03).
- A material provider without a current review is a readiness gap. A carved-out subservice organization without at least one CSOC is a readiness gap.

## Canonical model alignment

This uses `Provider`. The materiality fields, `boundary_treatment`, and the
`AssuranceReport` record are alignments requested on M0-D28.
