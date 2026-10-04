# Provider due diligence and assurance reports V1

Status: bounded backend contract for due diligence, assurance reports,
provider-specific coverage gaps, and advisory change-impact previews under
[#231](https://github.com/bdgrz/compliance/issues/231),
[#540](https://github.com/bdgrz/compliance/issues/540),
[#541](https://github.com/bdgrz/compliance/issues/541), and
[#542](https://github.com/bdgrz/compliance/issues/542). It builds on the
[manual provider register](provider-register-v1.md) and applies M0-D11 and
M0-D23. Users enter report facts, review evidence, and resolve gaps through
product workflows without an integration or optional automation. Authored report
facts preserve an issued auditor opinion as supplied; the platform does not
issue its own audit conclusion. Current delivery and acceptance evidence belong
to the linked issues.

## Readiness contribution

R1-08 [#489](https://github.com/bdgrz/compliance/issues/489) owns readiness
rules and their versions. Rules `readiness-rules/10` and later select provider
revisions at the assessment's as-of time, require a current review for each
material provider revision, require an effective linked CSOC for each carved-out
subservice provider, and link unresolved provider coverage gaps to distinct readiness
gaps. Source changes preserve prior assessment inputs and outcomes.

Related readiness rules select the as-of criteria edition, access-review scope
decisions, and effective technology revisions referenced by the approved
boundary. The latest integrity-verified workforce roster snapshot available by
the assessment time contributes only its ID, content hash, frozen time, and row
count, with gaps for missing or empty snapshots. Workforce completeness,
freshness, and source conflicts remain unassessed. Projection fences reject a
moved or lagging input view with a transient conflict, and unavailable source
history remains an explicit gap. These limits belong to readiness, rather than
the provider register's definition of a current due-diligence review.

## Records

One event-sourced tenant stream (`provider-assurance`) owns assurance reports,
provider reviews, and provider coverage gaps for providers of the same tenant.

An **assurance report** is a versioned authored fact: `report_kind` (`soc2_type1`, `soc2_type2`, `iso27001`, `questionnaire`), exact `issuer`, `scope`, `covered_services`, `period_start` (SOC 2 Type 2 only) and `period_end` (the report date for point-in-time kinds), `opinion` (`unqualified`, `qualified`, `adverse`, `disclaimer`; required for SOC 2, absent for ISO and questionnaires) with its `opinion_source`, `exceptions`, `complementary_controls`, declared `coverage_gaps`, an optional `bridge_letter` (`letter_date`, `covers_from`, `covers_through`) and a `citation`. Revisions require `expected_revision`; the provider cannot change. Lists default to 50 entries, accept limits of 1–200, and a provider retains at most 200 reports and 200 reviews.

A **provider review** is an immutable personal due-diligence conclusion: `reviewed_at`, `next_review_due`, `evidence_kind`, `conclusion` (`acceptable`, `acceptable_with_exceptions`, `not_acceptable`), `rationale`, `exceptions`, and evidence that is a same-provider report of the same kind (`assurance_report_id`) or a classified citation. The reviewer is the authenticated member, never a supplied name; a review is superseded by recording a later one, so history is the list. Rules: the next review is due after the review, within one year for a material or unclassified provider (M0-D11); a review is not future-dated, cannot precede its report period, and a report older than one year at the review date cannot support `acceptable` or `acceptable_with_exceptions`. The observed provider and report revisions are captured server-side and never rewritten.

## Stale coverage

A report is `current` for twelve months after `period_end` (the decided annual cadence) and `stale` afterwards. Its period coverage at an as-of date is `covers_as_of`, `bridged_only`, `bridge_expired`, `uncovered_after_period` or `not_yet_effective`. A bridge letter is management's statement for its interval only; it never makes a report complete, and a stale report stays stale whatever bridges it. Report-level `complete` requires current, `covers_as_of`, an issued opinion that is not qualified, adverse or disclaimed, no exceptions and no declared gaps.

The provider coverage read (`assurance-coverage`, optional `as_of`, default today UTC) evaluates from the authoritative streams and returns `not_required` (explicitly not material), `unresolved_materiality`, `no_review`, `not_acceptable`, `review_overdue`, `evidence_unrecorded`, `evidence_stale` or `current`, with `reasons` and a strict `complete`. `current` only means a review is in force on in-cycle evidence; every open point, such as a bridged-only interval, remains in `reasons`. It carries no sensitive report text.

## Provider coverage gaps and decisions ([#541](https://github.com/bdgrz/compliance/issues/541))

A `ProviderCoverageGap` preserves the exact provider revision, ClientService,
uncovered assertion, period, source kind, source ID and revision, description,
and recorder. Its identity and closure remain separate from any readiness gap
that cites it. Supported sources are assurance reports, provider reviews,
service commitments, system requirements, subservice responsibilities, and
governed evidence artifacts.

Closing a gap is an HTTP-only attributed decision with `expected_revision`,
`resolution: coverage_restored`, a rationale, and a verified source reference
that differs from the original or advances its revision. The server verifies
the cited source; the recorded human conclusion states why it restores coverage.
A linked R1-07 risk acceptance retains that decision's identity and expiry. The
link also requires authorization for the risk's owning program. It
does not close the provider gap or create a second acceptance authority.

## Provider change-impact preview ([#542](https://github.com/bdgrz/compliance/issues/542))

The read-only preview takes `expected_revision`, `change_kind` (`renewal`,
`material_change`, or `termination`), `effective_on`, and `change_summary`.
It returns bounded affected records for systems, data, controls, evidence, and
scope, with `pending_contexts`, `complete`, and a digest. Each context scans at
most 1,000 records and returns at most 200; unresolved relationships or overflow
remain explicit. This advisory observation does not renew or retire the provider,
approve the proposed change, update dependencies, or freeze an atomic snapshot.
Provider lifecycle decisions and complete downstream impact remain owning
R1-14 delivery scope.

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
| Record coverage gap | `POST .../providers/{provider_id}/coverage-gaps` | `bdgrz.provider.coverage_gap.record` |
| Read/list coverage gaps | `GET .../providers/{provider_id}/coverage-gaps[/{gap_id}]` | `bdgrz.provider.coverage_gap.get`, `bdgrz.provider.coverage_gaps.list` |
| Close coverage gap | `PUT .../providers/{provider_id}/coverage-gaps/{gap_id}` | none: attributed decision is HTTP-only |
| Link risk acceptance | `PUT .../providers/{provider_id}/coverage-gaps/{gap_id}/risk-acceptances` | none: acceptance link is HTTP-only |
| Preview provider change | `POST .../providers/{provider_id}/change-impact-previews` | `bdgrz.provider.change.preview` (read-only) |

Report and review lists use scoped cursors with limits of 1–200 and return a
transient conflict while the Fitz projection is behind. Coverage-gap lists
retain their own bounded contract. A retry of the same logical request returns
its original decision. A foreign tenant or unknown provider is not found without
disclosure. The `ProviderAssuranceV2` projection backfills and retains immutable
report revisions so readiness can resolve the exact report revision captured by
an as-of review; the report list continues to return the current revision.

## Not included

Governed upload and attachment integration (#196) and any field-level grants beyond the two read tiers above remain follow-up work. The versioned V2 projection replays the existing assurance event stream to seed revision history; provider and assurance source events are unchanged.
