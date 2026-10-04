# M0-D25: Client tenancy boundary, firm-staff affiliation, and data ownership

Status: accepted product decision, 2026-09-22. Decision owner: Jeff Repanich,
product owner. It closes [M0-D25 #123](https://github.com/bdgrz/compliance/issues/123).
The account and organization management model was clarified by the product owner
on 2026-10-04; the [business overview](../product-brief.md#organization-management-model)
records the direction alongside organization-level SaaS billing.
The technical realization belongs to ADR 0001 (tenancy and operator
provisioning), ADR 0002 (authorization and tenant isolation, M0-A04), and the
tenant identity ADR (M0-A07). This record states the product rules those ADRs
enforce. Where ADR 0001 still describes operator-only organization creation
and configuration-only operators, this decision supersedes it, and ADR 0001
must be amended when R1-15 adopts these rules.

| Question | Decision and rationale |
| --- | --- |
| Tenant boundary | The client `Organization` is the only tenant boundary. Every business record belongs to exactly one organization. There is no firm entity. One boundary keeps isolation proofs, retention, and export simple for a person or firm managing several organizations. |
| Platform-level content | Criteria catalog editions (M0-D02), firm templates (F1-04), the firm-staff directory, and the platform-operator roster live outside every tenant. A tenant references platform content by exact version or copies it with provenance. Client data never flows back to platform content except through an explicit, reviewed, de-identified contribution. This lets the firm reuse content without leaking one client's data to another. |
| Membership affiliation | Each canonical `Membership` records `client_personnel`, `firm_staff`, or `guest` (the directly hired external consultant in M0-D14). Existing backend `Member` types are implementation names pending the history-preserving canonical migration. A firm staff member's practice designation (advisory or attest) is recorded once, on the platform-level firm-staff directory, not in any client tenant. There is no firm entity to hold it. |
| Professional service access | Advisor or Attest access requires assignment to an accepted `ServiceEngagement` for that client. Removing the assignment, or ending the engagement, revokes that access immediately. Standing firm-staff membership grants no business-record access. An account holder acting as an organization administrator needs an explicit client-role membership grant under M0-D03; creating or managing a client organization does not grant professional-service authority. Independence walls still apply. |
| Firm-owned material | Advisory working notes live in the advisory compartment of the client organization. Attest staff cannot read that compartment (M0-D26). Where attest documentation lives is M0-D27's decision; the product owner's answer there is that attest workpapers stay in the attest team's own audit software, and M0-D27 records it. Retention of advisory material after the client offboards is a firm obligation that this decision does not settle; it is tracked in [#346](https://github.com/bdgrz/compliance/issues/346). |
| Organization creation | **Self-service.** Any signed-in platform user with a verified email address may create an organization and becomes its first Org Admin, with the client-personnel affiliation. This replaces R1-15's operator-only creation. Requiring a verified email (AUTH-02) keeps creation attributable. The creator's membership is the bootstrap administrator; no operator action is required. |
| Multiple organizations and management delegation | One account can create and manage many organizations for its own use or on behalf of clients. The account holder can hand most organization management to client personnel while continuing any responsibilities granted within that organization. Each organization remains its own tenant and hosted billing unit. Organization administration does not replace the engagement-assignment rules for professional Advisor or Attest work. |
| Suspension and offboarding | Only platform operators suspend, reactivate, or offboard an organization. Suspension blocks every member and assigned firm staff while preserving records. Offboarding, export, and disposition remain F1-01. |
| Platform operators | An operator is the platform super administrator for platform operations. An operator sees **metadata only**: tenant lifecycle, the administrator roster, and usage. An operator reads business records only by also holding a membership or an engagement assignment, like anyone else, and every such access is authorized and attributed through that membership. |
| Operator grant | An existing operator grants or revokes operator status **in the product**, and every grant and revocation is audit logged with actor, subject, time, and reason. There is no two-person rule. The configured operator IDs bootstrap the roster only while the platform has no operator, because an in-app grant needs an existing operator. After that, in-app grants and revocations are authoritative, and a configured ID never re-grants a revoked operator on restart. An operator cannot revoke the last remaining operator, so the platform cannot lock itself out. |
| Vocabulary | `tenant` is the technical name of a client organization everywhere: `tenant_id` in API paths, events, jobs, logs, and telemetry. Product and user-facing text says organization. They name the same thing, so there is no second concept. |
| Revisit triggers | Revisit the no-firm-entity decision if firm-level retention obligations cannot live inside client tenants (see #346), or if firm-wide reporting needs a record that belongs to no client. Multiple accounts or service providers managing organizations do not by themselves introduce a separate firm tenant. |

## Consequences for R1-15

- [R1-15 #127](https://github.com/bdgrz/compliance/issues/127) and
  [R1-15 backend #152](https://github.com/bdgrz/compliance/issues/152) must
  allow self-service organization creation by a verified signed-in user who
  becomes the first Org Admin. Suspend and reactivate stay operator-only.
- Operator status moves from configuration-only to an in-app grant and
  revocation by an existing operator, audit logged. Configuration seeds only
  the first operator, and the last operator cannot be revoked.
- Adding a standing firm-staff membership at provisioning grants no
  business-record access. Core organization administration uses explicitly
  granted client-role membership; Advisor or Attest access requires an
  engagement assignment (F1-07).

## Follow-up

- [#346 M0-D25a](https://github.com/bdgrz/compliance/issues/346): retention and
  handover of firm advisory material when a client offboards. It blocks F1-01.
