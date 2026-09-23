# M0-D18: Success measures, baselines, and targets

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D18 #75](https://github.com/bdgrz/compliance/issues/75).

No measured baseline exists today, and targets are not invented. The product
records these measures from the first client's first use. The 30-day baseline
and the targets are set in follow-up
[#340](https://github.com/bdgrz/compliance/issues/340).

| Measure | Product definition |
| --- | --- |
| Time to act on missing, stale, rejected, or overdue audit work | Median time from a work item entering an attention state to the first attributed action on it (claim, assign, delegate, escalate, comment, or submission). The attention states are: `missing` (required evidence not submitted at its due date), `stale` (accepted evidence past its freshness period), `rejected` (an evidence review rejection), and `overdue`. Page views are not tracked. |
| Evidence-request response time | Median time from an evidence request opening to its first submitted artifact (R2-03) |
| Parallel tracking | A self-reported count of spreadsheets or drive folders used to track audit work outside the product, captured in the first 30 days and again at the R2 exit |
| Overdue backlog | Count of overdue `AccountableWorkItem`s at each weekly digest |

The first three are the product-brief candidates. The fourth needs no extra
capture because the digest already produces it.

Instrumentation derives from existing attributed domain events and
projections: work-item state transitions and attributed actions. There is no
view tracking and no separate telemetry store for business data. The parallel
tracking measure is a dated, attributed self-report record. Each measure is reported per
organization and is never aggregated across clients without the cross-client
authorization defined in ADR 0007.
