# M0-D03: Platform roles, separation of duties, and team membership

Status: accepted product decision, 2026-09-22. Decision owner: Jeff Repanich,
product owner. It closes [M0-D03 #60](https://github.com/bdgrz/compliance/issues/60).
Enforcement is specified in ADR 0002 (M0-A04). Tenancy and firm-staff access
follow [M0-D25](m0-d25-client-tenancy.md).

| Question | Decision and rationale |
| --- | --- |
| Built-in role catalog | Four client roles: **Org Admin** (members, teams, grants, organization settings, SoD waivers), **Compliance Lead** (programs, scope, controls, approvals, and accepting imports), **Contributor** (owns and performs assigned controls, evidence, and tasks; may stage and preview imports but not accept them), and **Viewer** (read-only). Two firm-staff roles: **Advisor** and **Attest**. A small fixed catalog matches a small team and keeps authorization reviewable. Custom roles are out of scope for the first release. |
| Review and approval authority | Reviewer, management approver, control owner, and similar duties are `Responsibility` assignments, not roles. Roles answer what a member may do; responsibilities answer what they are expected to do. This replaces the domain model's earlier six-role list (reviewer, management approver, read-only advisor). |
| Current code alignment | The built-in RBAC in `BuiltInRbac` maps as follows: `tenant_administration` becomes Org Admin, `compliance_management` becomes Compliance Lead, and `compliance_participation` becomes Contributor. Viewer, Advisor, and Attest are new. The rename and new roles are delivered by R1-04b, with a history-preserving migration. |
| Scope hierarchy | A grant applies to an organization, a program, an engagement, or a deliberately shared resource. A grant on a wider scope covers the narrower scopes inside it. Advisor and Attest are granted **only** through assignment to an accepted `ServiceEngagement`, never as a standing organization grant (M0-D25). |
| Separation of duties | Self-review and self-approval are blocked by default. An Org Admin may record a time-bound, reason-required SoD `Waiver` (M0-D23) per record type. The Org Admin approves the waiver, which records scope, approver, rationale, and expiry. Every waived action is flagged in readiness results and in audit exports. Small teams can operate without hiding the conflict from auditors. |
| Workspaces | One workspace per organization. Programs and engagements provide sub-scoping. Several workspaces per organization is out of scope. |
| Invitations | Invitations are application-managed: the platform records the invitation and delivers it by email. The invitee accepts by signing in through any trusted issuer configured for that organization (M0-A07) with the invited, verified email. No provider-side invitation API (Auth0 or Entra) is required. |
| IdP group mapping | **Not in the first release.** Teams are platform-managed only. Mapping identity-provider groups to teams or grants moves to F1, alongside per-client identity providers (F1-06). R1-04c delivers platform teams without group mapping. |
| Responsibilities without sign-in | A responsibility may be held by a workforce `Person` who never signs in. When that person's work is completed outside the product, a signed-in member records it on their behalf. The record attributes both the member who entered it and the person who performed it. A team can hold a responsibility but can never be the performer or approver of record. |
| Firm and client role catalogs | Client personnel hold client roles through organization membership. Firm staff hold Advisor or Attest per engagement assignment. A firm staff member may also hold a client role only through an explicit membership grant, and independence walls still apply (M0-D26). |

## Consequences for blocked stories

- R1-04a/b/c/e, EN-01, and EN-04 use this catalog and the SoD waiver rule.
- R1-04c drops IdP group mapping from its first-release scope. The mapping is
  explicitly excluded, not deferred silently.
- R2-01, R2-05, and R2-11 assign duties through responsibilities, including
  responsibilities held by people who never sign in.
- Accepting and canceling an import requires Compliance Lead or Org Admin.
  Per the 2026-09-22 product direction, no import work is scheduled yet.

No remaining uncertainty requires a follow-up discovery issue.
