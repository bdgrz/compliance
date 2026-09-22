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
| Management compliance review | An occurrence with the minutes or agenda artifact and a management sign-off |

## Consequences

[T2-06 #36](https://github.com/bdgrz/compliance/issues/36) and
[T2-09 #39](https://github.com/bdgrz/compliance/issues/39) are closed as
covered by [T2-02 #32](https://github.com/bdgrz/compliance/issues/32).

The questions about observing a real review can't be answered before one
happens. Instead, reopen or re-file either story if real use of the recurring
control workflow shows a gap. The immutable `ManagementReviewSnapshot` in the
backlog becomes an occurrence's frozen evidence set, not a separate aggregate.
