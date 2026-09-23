# M0-D12: Policy audiences, acknowledgement, and training evidence

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D12 #69](https://github.com/bdgrz/compliance/issues/69).

## Audience rules

Audiences are always sets of **workforce people** from the R1-11 roster, never
platform members. A workforce person who never signs in can still be in scope.
In that case an attributed member records the acknowledgement on their behalf,
following the rule the product owner set under M0-D03 on the same day.

| Policy kind | Audience rule |
| --- | --- |
| `core_security` | Every in-scope workforce person: employees and contractors with access to in-scope systems or data. |
| `role_targeted` | Workforce people whose roster team (the team or department attribute on the workforce roster, per M0-D06) is named on the policy version. |

Each approved policy version carries exactly one `audience_kind` and, for
`role_targeted`, a non-empty list of roster teams. An audience is never inferred
from application access, platform team membership, or the IdP.

## Campaign audience and amendments

- **Launch:** a `PolicyDistributionCampaign` freezes its launch audience by
  evaluating the rule against the roster at launch.
- **Changes during the campaign:** later roster changes never edit the frozen
  launch audience. Instead they add attributed **audience amendment** entries,
  each with the person, reason (`joiner`, `mover_in`, `mover_out`, or
  `leaver`), roster observation, and time.
- **Evidence:** the launch audience plus its amendments makes up the evidence
  population.

## Acknowledgement

- **Language:** the acknowledgement records that the person has read,
  understood, and agrees to comply with the exact policy version, identified
  by version and content hash. The text is a product default; an Org Admin
  may replace it per organization, and the acknowledgement stores the exact
  text shown.
- **Cadence:** new joiners have 30 days from their start date. Everyone else
  re-acknowledges annually, and whenever a new major policy version is
  approved.
- **States:** `pending`, `acknowledged`, `overdue`, `excepted`, or `removed`.

## Reminders

- **Members:** a pending acknowledgement for a workforce person who is a
  platform member follows the M0-D15 rules: in-app reminders, the weekly
  digest, and escalation at 7 days overdue.
- **Non-members:** a pending acknowledgement for a workforce person without a
  membership becomes a work item assigned to the campaign owner, who collects
  the acknowledgement and records it on the person's behalf. The same
  escalation applies. The product sends no email to non-members.

## Training

- A `TrainingRequirement` names the security-awareness course, the audience
  rule (same vocabulary as above), and the cadence: at hire within 30 days,
  then annually.
- The delivery source is `manual` or `lms_export`. A completion is a record
  with person, course, completed date and source, and the source artifact (an
  LMS export row or an uploaded certificate) is the evidence. An LMS connector
  is out of scope (M0-D20).

## Exceptions and joiner/mover/leaver

- **Exceptions:** an exception is a `Waiver` (M0-D23) with scope, approver
  (Compliance Lead), reason, and expiry of at most 12 months. Waived people
  count as `excepted`, never as `acknowledged`.
- **Joiners:** a joiner is added by amendment to every active campaign whose
  rule matches them, whether `core_security` or `role_targeted`. Their item is
  due 30 days after their start date.
- **Movers:** a mover is added by amendment to active `role_targeted` campaigns
  of the new team (`mover_in`). They are removed from campaigns of the old team
  (`mover_out`), and earlier completions keep their history.
- **Leavers:** a leaver moves to `removed` in open campaigns and keeps their
  history.

## Auditor evidence

The evidence is:

- the campaign's frozen launch audience and amendments;
- each person's state;
- the exact acknowledged version and hash.

It is exported in the M0-D17 default formats. Confirming the format with the
actual audit firm is part of
[#339](https://github.com/bdgrz/compliance/issues/339).

## Consequences

[R2-10 #53](https://github.com/bdgrz/compliance/issues/53) and its backend child
[#283](https://github.com/bdgrz/compliance/issues/283) adopt these audience,
amendment, cadence, state, reminder, and exception rules.
