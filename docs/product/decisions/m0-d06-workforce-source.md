# M0-D06: Authoritative workforce source and NHI ownership

Status: accepted product decision, 2026-09-22. Decision owner: Jeff Repanich,
product owner. It closes [M0-D06 #63](https://github.com/bdgrz/compliance/issues/63).
Concept ownership follows [M0-D22](m0-d22-canonical-ownership.md): `Person` and
`WorkRelationship` belong to R1-11a, and `ServiceIdentity` belongs to R1-11c.

| Question | Decision and rationale |
| --- | --- |
| Authoritative source and precedence | An HRIS roster export is authoritative for people, employment status, and manager. The identity-provider directory corroborates it: a disagreement is shown as a conflict for an attributable reconciliation decision, never silently resolved. HR owns employment facts, and the IdP owns sign-in accounts. |
| Manual fallback | When the organization has no HRIS, a manually maintained roster in the product is the accepted authoritative source. Each entry is attributed to the member who recorded it. |
| Minimum worker attributes | Stable source worker identifier, display name, work email, worker type (employee, contractor, external collaborator), lifecycle status, start and end dates, direct manager, and department or team. Anything else is excluded until a story needs it. |
| Privacy boundary | Restricted fields: personal contact details, employment status reason, and the manager chain. They belong to a restricted workforce field class (ADR 0002 field-restriction hook) and are readable only with an explicit grant, by default Org Admin and Compliance Lead. Lists, exports, search, and counts redact them. Server-side rules (for example reviewer assignment) may use them without disclosing them. |
| Joiner, mover, leaver | Joiners, movers, and leavers are observed by comparing accepted roster versions. Contractors are workforce people with worker type contractor and follow the same rules. An external collaborator with access to a reviewed system is recorded as a workforce person with worker type external collaborator and an accountable internal sponsor. Observations create compliance work; they never grant platform access, revoke provider access, or decide a review outcome. |
| NHI ownership | Every non-human identity (`ServiceIdentity`) has exactly one accountable owner, a person or a team, plus an approved purpose and a review date at most one year out. Ownership is reviewed at least annually. An NHI with an ended owner relationship or an expired review date shows as unowned work. |
| Current direction | Per the 2026-09-22 product direction, the canonical workforce shape and its storage come first. No roster import or HRIS integration is built yet. |

## Consequences for blocked stories

- R1-11a records the roster manually first. Its import portion stays excluded
  until [#347](https://github.com/bdgrz/compliance/issues/347) records the first
  client's HRIS export shape and import work is scheduled.
- R2-06 correlates access subjects to accepted workforce people and NHI owners
  under these rules.
- R2-10 applies the joiner, mover, and leaver rules to acknowledgement and
  training campaigns.

## Follow-up

- [#347 M0-D06a](https://github.com/bdgrz/compliance/issues/347): the first
  client's HRIS and roster export shape. This is an organization-specific
  fact, not a product rule.
