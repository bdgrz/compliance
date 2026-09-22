# M0-D18: Success measures, baselines, and targets

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D18 #75](https://github.com/bdgrz/compliance/issues/75).

No measured baseline exists today, and targets are not invented. The product
records these measures from the first client's first use. The 30-day baseline
and the targets are set in follow-up
[#340](https://github.com/bdgrz/compliance/issues/340).

| Measure | Product definition |
| --- | --- |
| Time to identify missing, stale, rejected, or overdue audit work | Median time from a work item becoming overdue, or its evidence being rejected, to its first view by the assignee (M0-D15 projection events) |
| Evidence-request response time | Median time from an evidence request opening to its first submitted artifact (R2-03) |
| Parallel tracking | A self-reported count of spreadsheets or drive folders used to track audit work outside the product, captured in the first 30 days and again at the R2 exit |
| Overdue backlog | Count of overdue `AccountableWorkItem`s at each weekly digest |

The first three are the product-brief candidates. The fourth needs no extra
capture because the digest already produces it.

Instrumentation derives from existing attributed events and projections; there
is no separate telemetry store for business data. Each measure is reported per
organization and is never aggregated across clients without the cross-client
authorization defined in ADR 0007.
