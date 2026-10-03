# Provider due diligence and assurance reports V1

Implements the due-diligence and assurance-report slice of [#231](https://github.com/bdgrz/compliance/issues/231) on top of the [manual provider register](provider-register-v1.md), using M0-D11 and M0-D23. Canonical CSOC-to-provider links (#540) are added in PR #561; coverage-gap resolution (#541) and reassessment, renewal and termination impact (#542) remain separate work. Nothing here is an approval, an audit conclusion or a readiness gap (#489 owns readiness rules). Readiness rules v4 acknowledges the provider family as `not_assessed` with an explicit gap until its historical provider, review and CSOC rules are delivered under #489; that status makes no claim about provider coverage. Readiness assessments also resolve the criteria edition selected at their requested as-of time.

## Records

One event-sourced tenant stream (`provider-assurance`) owns two record kinds for providers of the same tenant.

An **assurance report** is a versioned authored fact: `report_kind` (`soc2_type1`, `soc2_type2`, `iso27001`, `questionnaire`), exact `issuer`, `scope`, `covered_services`, `period_start` (SOC 2 Type 2 only) and `period_end` (the report date for point-in-time kinds), `opinion` (`unqualified`, `qualified`, `adverse`, `disclaimer`; required for SOC 2, absent for ISO and questionnaires) with its `opinion_source`, `exceptions`, `complementary_controls`, declared `coverage_gaps`, an optional `bridge_letter` (`letter_date`, `covers_from`, `covers_through`) and a `citation`. Revisions require `expected_revision`; the provider cannot change. Every list is bounded to 50 entries and a provider retains at most 200 reports and 200 reviews.

A **provider review** is an immutable personal due-diligence conclusion: `reviewed_at`, `next_review_due`, `evidence_kind`, `conclusion` (`acceptable`, `acceptable_with_exceptions`, `not_acceptable`), `rationale`, `exceptions`, and evidence that is a same-provider report of the same kind (`assurance_report_id`) or a classified citation. The reviewer is the authenticated member, never a supplied name; a review is superseded by recording a later one, so history is the list. Rules: the next review is due after the review, within one year for a material or unclassified provider (M0-D11); a review is not future-dated, cannot precede its report period, and a report older than one year at the review date cannot support `acceptable` or `acceptable_with_exceptions`. The observed provider and report revisions are captured server-side and never rewritten.

## Stale coverage

A report is `current` for twelve months after `period_end` (the decided annual cadence) and `stale` afterwards. Its period coverage at an as-of date is `covers_as_of`, `bridged_only`, `bridge_expired`, `uncovered_after_period` or `not_yet_effective`. A bridge letter is management's statement for its interval only; it never makes a report complete, and a stale report stays stale whatever bridges it. Report-level `complete` requires current, `covers_as_of`, an issued opinion that is not qualified, adverse or disclaimed, no exceptions and no declared gaps.

The provider coverage read (`assurance-coverage`, optional `as_of`, default today UTC) evaluates from the authoritative streams and returns `not_required` (explicitly not material), `unresolved_materiality`, `no_review`, `not_acceptable`, `review_overdue`, `evidence_unrecorded`, `evidence_stale` or `current`, with `reasons` and a strict `complete`. `current` only means a review is in force on in-cycle evidence; every open point, such as a bridged-only interval, remains in `reasons`. It carries no sensitive report text.

## Sensitivity and authority

Citation metadata must be explicitly classified `public`, `internal`, `confidential` or `restricted`, with an optional same-tenant governed `artifact_id` that is verified but not fetched. A report is treated as restricted unless its citation is `public` or `internal`. Reads and writes use the register's authorizer: organization-wide `tenant.access` reads, organization-wide `provider_inventory.manage` writes. Readers without `provider_inventory.manage` receive restricted reports with exception, control, gap and service text and the citation withheld (`redacted: true`; counts, kind, issuer, period and opinion remain), and reviews with a confidential or restricted evidence citation withheld; review conclusions stay shareable. No new permission or grant backfill is added.

## HTTP and MCP operations

| Operation | HTTP | MCP |
| --- | --- | --- |
| Record report | `POST /api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-reports` | `bdgrz.provider.assurance_report.record` |
| Revise report | `PUT .../providers/{provider_id}/assurance-reports/{report_id}` | `bdgrz.provider.assurance_report.revise` |
| List reports | `GET .../providers/{provider_id}/assurance-reports` | `bdgrz.provider.assurance_reports.list` |
| Record review | `POST .../providers/{provider_id}/reviews` | none: personal sign-offs are HTTP-only |
| List reviews | `GET .../providers/{provider_id}/reviews` | `bdgrz.provider.reviews.list` |
| Coverage | `GET .../providers/{provider_id}/assurance-coverage?as_of=` | `bdgrz.provider.assurance_coverage.get` |

Lists use scoped cursors with limits of 1-200 and return a transient conflict while the Fitz projection is behind. A retry of the same logical request returns its original decision. A foreign tenant or unknown provider is not found without disclosure.

## Not included

Governed upload and attachment integration (#196), report revision-history reads and any field-level grants beyond the two read tiers above remain follow-up work. Events and the `ProviderAssuranceV1` projection are additive; no existing schema changes.
