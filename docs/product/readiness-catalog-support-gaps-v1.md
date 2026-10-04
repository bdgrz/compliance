# Catalog support limitations in readiness

This backend contract implements [#584](https://github.com/bdgrz/compliance/issues/584)
under the readiness capability [#489](https://github.com/bdgrz/compliance/issues/489).
It follows [M0-D01](decisions/m0-d01-program-targets.md),
[M0-D02](decisions/m0-d02-criteria-content.md), and the
[criteria catalog contract](criteria-catalog-v1.md).

## Business outcome

A compliance lead sees the product's declared support limitations when running
a manual readiness assessment. A complete numbered-criterion catalog can still
have partial points of focus or unsupported category workflows. These limits
remain visible alongside the program's other readiness gaps.

The platform edition currently declares partial points of focus for all five
Trust Services categories and unsupported Privacy lifecycle coverage. An
assessment whose approved scope selects all five categories records six
catalog support gaps. Security-only scope records the Security limitation.
Privacy disclosure does not deliver the lifecycle capability tracked in
[#349](https://github.com/bdgrz/compliance/issues/349).

## Edition and scope

The assessment resolves the program's exact criteria edition selected at its
`as_of` time. It uses that edition's declared support metadata, including the
original limitation note.

Categories come from the existing readiness selection of boundary versions:
for each boundary, select the version approved by `as_of` and effective on the
as-of UTC date. The distinct union of those versions' recorded categories
determines which limitations apply. A later approval or future effective date
does not introduce a category into an earlier assessment.

An absent edition retains the existing catalog-not-assessed gap. An absent
approved/effective boundary retains the existing missing-boundary gap and does
not supply inferred category defaults. Truncated source reads retain their
explicit incompleteness gap; source projection lag or movement retains its
transient conflict behavior.

## Recorded gap

| Field | Value |
| --- | --- |
| `kind` | `catalog_support_gap` |
| `rule_id` | `catalog_support_declared` |
| `subject` | Declared category and code, separated by `\|`. |
| `explanation` | The declared limitation note. |
| `sources` | The exact catalog edition ID and its edition label, with source kind `criteria_catalog_edition`. |
| `gap_id` | Deterministic identity incorporating program, rule, exact edition, category and code. |

Each edition has at most one declared limitation per category/code key.
Repeated scope categories do not create duplicate gaps. Equivalent reassessments
retain gap identities so their plans can carry forward. A different edition
gets different gap identities; an old edition's plan cannot silently apply to
the replacement edition.

## Reproducibility and delivery

New assessments use `readiness-rules/11`. Their input fingerprint includes the
selected category set and applicable declared support metadata in deterministic
order, including the note and edition provenance. An applicable note change
changes the fingerprint. Reordering equivalent metadata or category inputs
does not.

The existing assessment event retains the exact edition, rule version,
fingerprint and gap records. Ledger replay and Fitz projections serve that
recorded result. Earlier assessments retain their original rules and content;
current catalog or boundary changes do not recalculate them. Existing current
plan overlays remain available through the gap read contract.

Assessment detail and gap-list operations expose these records through the
existing authorized HTTP and read-only MCP contracts. Existing kind, rule and
subject filters apply. Running the manual assessment and personal management
approvals retain their existing HTTP-only boundaries.

A plan, annotation or acknowledgment records management's response; it does
not erase the product limitation or convert it to a met rule. Readiness remains
a management assessment with explicit inputs and gaps, never an auditor
opinion. Severity, target-stage/control-link filters and other unresolved
policies stay with [#492](https://github.com/bdgrz/compliance/issues/492).
