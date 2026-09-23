# M0-D04: Existing readiness material and import order

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D04 #61](https://github.com/bdgrz/compliance/issues/61).

## Decision

The first client has **no existing readiness material**. It has no control
spreadsheet, mapping workbook, policy set, evidence archive, risk register,
vendor list, or consultant findings to adopt. The program starts from
governed records authored in the product.

Product direction recorded the same day: no import or integration work is
scheduled until the normalized data shape (M0-D28) and the storage internals
(M0-A01, M0-A02, M0-A03, and M0-A05) are settled. Imported records later land
in that same canonical shape, so the shape is decided first.

| Question | Decision and rationale |
| --- | --- |
| Sample files | None exist for the first client. Collection moves to [#338](https://github.com/bdgrz/compliance/issues/338), for the first client that brings material. |
| Stable identifiers | Not applicable yet. When material arrives, every imported row keeps its original source identifier as provenance, separate from the product's own identity (the M0-D28 source-observation vs. governed-fact distinction). A missing source identifier is recorded as `source_identifier_absent` and never synthesized from row order. |
| Owner matching | The default rule, confirmed later against real files in #338: a named owner matches a workforce person only by exact normalized email. A name-only or ambiguous match stays an `unresolved_owner` reference that a person resolves. No member or workforce person is created from an import row. |
| Consultant attribution | Consultant-authored content keeps `imported_from` provenance (source file, row, stated author text) and is never attributed to a platform actor who did not act. The accepting member is the actor of the acceptance decision only. |
| Slice order | Deferred to #338. The ranking must come from real material, not an assumption. |
| Manual re-entry | For the first client, all readiness content is authored in the product. |

## Consequences

- The R1-09 family moves from P0 to P1: [R1-09 #47](https://github.com/bdgrz/compliance/issues/47),
  [R1-09a #98](https://github.com/bdgrz/compliance/issues/98),
  [R1-09b #99](https://github.com/bdgrz/compliance/issues/99),
  [R1-09c #100](https://github.com/bdgrz/compliance/issues/100), and
  [R1-09d #101](https://github.com/bdgrz/compliance/issues/101). The
  product-backlog contract still requires the R1 exit without imports.
- Import mechanics stay governed by ADR 0005 (M0-A06). Its spike and
  implementation belong to EN-05 and are not scheduled now.
- R1-09a is blocked by #338 because its record formats depend on real files.
