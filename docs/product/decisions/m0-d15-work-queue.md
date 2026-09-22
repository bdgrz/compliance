# M0-D15: Daily work queue, reminders, and escalation

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D15 #72](https://github.com/bdgrz/compliance/issues/72).

## Source workflows on day one

`AccountableWorkItem` is a projection (ADR 0007, M0-A05), not a second source of
workflow truth. Its first sources are:

| Source | Work item |
| --- | --- |
| Controls (R2-01, R2-04) | A control occurrence that is due or overdue |
| Evidence requests (R2-03) | An open request for evidence |
| Reviews and approvals | A pending EN-04 decision assigned to the person or their team |
| Findings (R2-07) | Corrective actions |
| Access review campaigns (R2-06) | Pending reviewer decisions and remediations |
| Policy campaigns (R2-10) | Pending acknowledgements |

Every item links to its exact source record. Completing work happens in the
source workflow, and the item then disappears from the queue.

## Ordering

Items are ordered by:

1. due date ascending, which puts overdue items first;
2. then by materiality from the source record, `high`, then `medium`, then
   `low`, with items whose source has no materiality sorting after `low`;
3. then by item creation time;
4. then by work-item identifier as the final tie-break.

Items without a due date sort after every dated item. The order is
deterministic for a given projection as-of time.

## Assignment actions

| Action | Who may perform it |
| --- | --- |
| `claim` an unassigned team item | Any member of that team with the source workflow's required role |
| `assign`, `reassign` | Compliance Lead or Org Admin |
| `delegate` | The current assignee, to someone eligible under the source workflow |
| `escalate` | Any assignee, and the system (see below) |

Every action is attributed and checked against the separation-of-duties rules
the product owner set under M0-D03 on the same day. An item cannot be assigned to a person the source workflow would
reject.

## Reminders, digest, and escalation

- **Reminders:** in-app, at the due date and when an item becomes overdue.
- **Digest:** one weekly email per member, listing their overdue items and
  items due in the next 7 days. It is the only email for R2. Per-item email is
  not sent. Members may opt out of the digest but not out of in-app reminders.
- **Escalation:** an item 7 days overdue escalates automatically to the
  Compliance Lead, recorded as a system-actor escalation. Escalation adds
  visibility; it does not reassign the item.

## Consequences

[R2-11 #54](https://github.com/bdgrz/compliance/issues/54),
[R2-11a #119](https://github.com/bdgrz/compliance/issues/119), and
[#284](https://github.com/bdgrz/compliance/issues/284) adopt these sources,
ordering, actions, and notification rules. R2-11b and R2-11c follow the same
rules.
