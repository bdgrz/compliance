# M0-D14: Readiness-consultant collaboration

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D14 #71](https://github.com/bdgrz/compliance/issues/71).

## Channel

- **Our firm's consultants** work **in the product** as `Advisor`-role
  assignees of an accepted `ServiceEngagement` for that client (M0-D25).
  Removing the assignment revokes access immediately.
- **External consultants hired directly by a client** get a scoped, revocable
  **guest membership**:
  - it is limited to the programs the Org Admin grants;
  - it has no approval rights;
  - it expires on a required end date.

  No export handoff package, secure link, or third-party platform is built
  for R2.

## Draft sharing

An `AdvisorHandoff` is a bounded selection of exact draft record versions that
the Compliance Lead marks `ready_for_review`. Advisors and guests see only
records in an open handoff, plus records that are already approved. Drafts that
were never marked ready stay invisible to them. Closing or revoking a handoff
removes the access; history stays.

## "Consultant validated"

`AdvisorFeedback` has the kinds `comment`, `change_request`, and `validated`.

- `validated` is a distinct attributed review of an exact record version. It
  **is not** an approval: it never changes the record's approval state, and
  it never satisfies a separation-of-duties or approval requirement.
- An internal approval remains an EN-04 decision by an authorized member.
- Validation is invalidated when a new version supersedes the validated one.

## Feedback

Feedback records its author (the Advisor or guest member), time, target record
version, and kind. It is immutable after posting; a correction is a new entry
that links back. Revocation removes future access, never past attribution.

## Consequences

[R2-08 #22](https://github.com/bdgrz/compliance/issues/22) and
[#275](https://github.com/bdgrz/compliance/issues/275) adopt in-product
review, `ready_for_review` handoffs, and the validation semantics. The scoped
guest membership follows the role catalog and grant scopes in ADR 0002
(M0-A04).
