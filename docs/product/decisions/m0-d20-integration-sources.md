# M0-D20: Inventory, access, and evidence integration sources

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D20 #77](https://github.com/bdgrz/compliance/issues/77).

## Decision

No connectors are built in R1 or R2, and no import or integration work is
scheduled until the canonical data shape (M0-D28) and the storage internals
are settled. [T2-08 #38](https://github.com/bdgrz/compliance/issues/38) and
[T2-08a #137](https://github.com/bdgrz/compliance/issues/137) stay P2
hypotheses. Manual effort per source is measured during the first readiness
engagement, and connectors are ranked from that data in
[#342](https://github.com/bdgrz/compliance/issues/342).

The first candidate sources are the first reviewed systems:

- AWS accounts;
- the GitHub organization;
- the identity-provider tenant;
- core SaaS admin access (for example Slack and Jira).

## Integration template

Every candidate source must answer these ten questions before a connector is
proposed. They come from the gap analysis.

1. Which business question does the source answer?
2. Is it authoritative, corroborating, or discovery-only?
3. What are its stable identifiers, scope boundaries, timestamps, pagination,
   and deletion or tombstone behavior?
4. How is a complete population proven, and how is partial collection
   detected?
5. How do records match without silently merging identities or systems?
6. How do changes become reviewable proposals rather than automatic truth?
7. What are the least-privilege, credential-custody, rate-limit, retry, and
   revocation models?
8. What freshness and reconciliation cadence is required, and who owns it?
9. Which raw snapshots and normalized facts must be retained for audit proof?
10. Which manual path remains when the integration is unavailable?

Any future connector writes source observations into the M0-D28 canonical shape
through the ADR 0005 staged, all-or-nothing acceptance. A connector never writes
governed facts directly.
