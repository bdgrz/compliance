# M0-D19: Periodic governance and management reviews

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D19 #76](https://github.com/bdgrz/compliance/issues/76).

## Decision

Periodic policy, risk, vendor, and management compliance reviews are
**ordinary recurring controls**. Each is a control with a cadence (R2-01),
an expected `ControlOccurrence` population (T2-02), evidence, and an
attributed sign-off. No dedicated review surface is built.

## Examples

| Review | Control shape |
| --- | --- |
| Annual policy review | An occurrence per policy, with evidence of the approved new or unchanged policy version (R2-02) |
| Annual risk reassessment | An occurrence with evidence of the `RiskAssessment` version (M0-D10 cadence) |
| Annual vendor review | An occurrence per material provider (M0-D11), with evidence of the assurance-report review |
| Management compliance review | An occurrence, detailed below |

## Management compliance review as an occurrence

The occurrence's frozen evidence set replaces the separate
`ManagementReviewSnapshot` aggregate. It binds:

- the exact R1-08 readiness assessment version;
- the T2-04 monitoring measure versions;
- the agenda or minutes artifact.

Management's sign-off is an EN-04 decision on the occurrence, with outcome
`approved`, `action_requested`, or `deferred` and a rationale. Each requested
action is created as a `Finding` with a corrective action (R2-07), linked to
the occurrence. It therefore appears in the accountable work queue (M0-D15)
and is tracked to completion there. Later data changes never alter the frozen
evidence set.

## Consequences

[T2-06 #36](https://github.com/bdgrz/compliance/issues/36) and
[T2-09 #39](https://github.com/bdgrz/compliance/issues/39) are closed as
covered by:

- [T2-02 #32](https://github.com/bdgrz/compliance/issues/32), for the recurring
  occurrences;
- R2-07, for management-requested actions.

The questions about observing a real review can't be answered before one
happens. Instead, reopen or re-file either story if real use of the recurring
control workflow shows a gap. The M0-D23 readiness-ownership text and the
backlog entries for T2-06 and T2-09 are updated to match.
