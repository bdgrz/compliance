# M0-D12: Policy audiences, acknowledgement, and training evidence

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D12 #69](https://github.com/bdgrz/compliance/issues/69).

## Audience rules

A `PolicyDistributionCampaign` freezes its audience from the workforce roster
(R1-11) when it launches. The audience is always a set of workforce people,
never platform members. A person who never signs in can still be in scope,
and an attributed member records the acknowledgement on their behalf (M0-D03).

| Policy kind | Audience rule |
| --- | --- |
| `core_security` | Every in-scope workforce person: employees and contractors with access to in-scope systems or data. |
| `role_targeted` | Members of the named platform teams or workforce role groups recorded on the policy version. |

Each approved policy version carries exactly one `audience_kind` and, for
`role_targeted`, a non-empty team list. An audience is never inferred from
application access or the IdP.

## Acknowledgement

- **Language:** the acknowledgement records that the person has read,
  understood, and agrees to comply with the exact policy version, identified
  by version and content hash. The text is a product default; an Org Admin
  may replace it per organization, and the acknowledgement stores the exact
  text shown.
- **Cadence:** new joiners have 30 days from their start date. Everyone else
  re-acknowledges annually, and whenever a new major policy version is
  approved.
- **Reminders:** reminders go through the M0-D15 work queue: in-app, the
  weekly digest, and escalation to the Compliance Lead at 7 days overdue.
- **States:** `pending`, `acknowledged`, `overdue`, `excepted`, or `removed`.
  A leaver moves to `removed` with their earlier completions kept.

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
- **Joiners:** a joiner observed from the roster enters every active
  `core_security` campaign, due 30 days after their start date.
- **Movers:** a mover whose team changes re-acknowledges the
  `role_targeted` policies of the new team.
- **Leavers:** a leaver drops out of open campaigns and keeps their history.

## Auditor evidence

The campaign's frozen audience, each person's state, and the exact
acknowledged version and hash make up the completion evidence, exported in the
M0-D17 default formats. Confirming the format with the actual audit firm is
part of [#339](https://github.com/bdgrz/compliance/issues/339).

## Consequences

[R2-10 #53](https://github.com/bdgrz/compliance/issues/53) and its backend child
[#283](https://github.com/bdgrz/compliance/issues/283) adopt these audience,
cadence, state, and exception rules.
