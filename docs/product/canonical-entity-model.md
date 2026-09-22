# Canonical entity model

Status: approved domain contract, 2026-09-22. Decision owner: Jeff Repanich
(product owner and tech lead). Approved under
[M0-D28 #139](https://github.com/bdgrz/compliance/issues/139); see
[Approval record](#approval-record).

This document defines a provider-neutral vocabulary for people, organizations,
workforce relationships, identities, accounts, applications, and access. It is
a conceptual and interoperability model, not a database schema. It does not
define aggregate roots, aggregate ownership, command boundaries, storage
layout, or service boundaries; those are later design decisions.

The model deliberately follows established standards where they define useful
semantics:

- SCIM 2.0 for provisioned users, enterprise-user attributes, groups, resource
  identifiers, and lifecycle metadata;
- OpenID Connect for authentication identity as an issuer and subject pair;
- NIST RBAC for users, roles, permissions, assignments, hierarchies, and
  separation-of-duty constraints;
- W3C PROV for sources, observations, derivations, agents, and activities.

No one standard covers the whole domain. The canonical model composes those
standards without changing their meanings. Provider-specific objects and
attributes remain source projections or namespaced extensions.

Every standards claim in this model is governed by the
[public source reference and standards-use policy](source-reference-policy.md).
Only sources in its approved register may shape this model. Sources whose use
has not been cleared for this Apache-2.0 product are excluded rather than used
as informal inspiration.

The standards names in the entity tables refer to the exact editions linked
under [Standards references](#standards-references). `SCIM` means RFC 7643
(and RFC 7644 only for protocol behavior), `OpenID Connect` means Core 1.0
incorporating errata set 2, `NIST RBAC` means NIST IR 6192, `W3C PROV` means
the April 2013 PROV-O Recommendation, and `IETF hardware model` means RFC 8348.
A table entry labeled as an original product decision is not a
standards-conformance claim. No
schema, standard prose, or test corpus is copied into this catalog.

## Modeling principles

1. A real person, their relationship to an organization, their login identity,
   and each account they hold are different records.
2. An application product and a deployable or reviewable instance of that
   product are different records.
3. Groups collect principals. Roles collect permissions. They are not synonyms.
4. An entitlement describes access that can be granted. A grant records that
   the access was assigned. Effective access is a derived, explainable path.
5. Every canonical record has a platform-generated immutable ID. Source IDs,
   email addresses, employee numbers, names, slugs, and usernames are alternate
   identifiers or attributes, never canonical keys.
6. Imported observations do not silently overwrite governed facts. Authority,
   provenance, observed time, effective time, and reconciliation decisions are
   explicit.
7. Tenant ownership is explicit on every tenant-owned record and relationship.
   Cross-tenant identity correlation never implies cross-tenant visibility.
8. Standards-compatible does not mean copying an external JSON document into
   the domain. Adapters preserve the source payload and map it to canonical
   records with attributable decisions.

## Canonical entity families

### Parties and workforce

| Entity | Canonical meaning | Important attributes and relationships | Standards alignment |
| --- | --- | --- | --- |
| `Organization` | A legal, business, or administrative organization. A client organization may also be the product tenant. | immutable ID, legal/display name, type, status, parent organization, alternate identifiers | SCIM enterprise `organization`; W3C PROV `Organization` |
| `OrganizationalUnit` | A department, division, cost center, team-like business unit, or other node in an organization structure. It is not an access-control group. | organization, type, name, parent unit, effective interval, source identifiers | SCIM enterprise `organization`, `division`, `department`, and `costCenter`, normalized into governed records |
| `Person` | A natural person, independent of employment, login, or account status. | names, contact points, locale/time zone, status, alternate identifiers | SCIM User name/contact shapes; W3C PROV `Person` |
| `WorkRelationship` | A person's effective-dated employment, contract, internship, advisory, or other relationship with an organization. “Employee” is a person with an active employment relationship, not a subtype or duplicate person. | person, organization, worker type, employee number, start/end dates, status, manager relationship, organization units, job profile | SCIM EnterpriseUser attributes and manager relationship |
| `JobProfile` | A governed job or position definition used for workforce classification and access expectations. | code, title, family, level, duties, organization applicability | Organization-owned reference data; not an authorization role |
| `Responsibility` | Accountable work assigned to a person, organization unit, team, or platform member for a scope and interval. The assignee need not be able to sign in. | assignee, responsibility type, governed object/scope, effective interval, source | Distinct from RBAC authorization; may use W3C PROV roles when attributing an activity |

Do not store `Employee`, `Contractor`, or `Manager` as competing person types.
Worker type belongs to `WorkRelationship`; management is an effective-dated
relationship between work relationships. This supports rehire, simultaneous
relationships, manager changes, and contractors who later become employees.

Workforce source authority (M0-D06): the organization's HR system of record
is the authoritative `SourceSystem` for `Person`, `WorkRelationship` status,
and manager relationships; an identity-provider directory is corroborating,
and its disagreements are reconciliation items, not overwrites. When no HR
system exists, the manually governed roster is authoritative. Personal
contact points, employment-status reason, and the manager chain are restricted
workforce fields; their visibility is decided
by the authorization field-restriction mechanism (M0-A04), not by this catalog.

A `Responsibility` may be held by a `Person` who never signs in. Work that
person performs outside the platform is recorded by a signed-in member acting
on their behalf: the action's actor is the recording `Membership`, and the
responsible `Person` is retained as a separate on-behalf-of attribution. Neither
is replaced by the other, and no platform activity is fabricated for the
person.

### Identity and platform access

| Entity | Canonical meaning | Important attributes and relationships | Standards alignment |
| --- | --- | --- | --- |
| `FederatedIdentity` | A subject asserted by an identity provider. | exact issuer, subject, provider, first/last observed, claims snapshot, optional person correlation | OpenID Connect `(iss, sub)`; email is not identity |
| `PlatformUser` | The platform-local security subject to which sign-in identities bind. It exists even if identity providers change. | status, federated identities, optional person correlation | Local security subject; deliberately narrower than SCIM User |
| `Membership` | A platform user's effective-dated affiliation with one tenant organization. | platform user, organization, affiliation (`client_personnel`, `firm_staff`, or `guest`), lifecycle, invitation/provisioning source | Tenant-local relationship; analogous to, but not represented as, a SCIM Group |
| `Team` | A platform-managed collection used for work assignment or platform authorization. | organization, purpose, members, effective membership intervals | SCIM Group-shaped collection; semantics owned by Compliance |
| `AccessRole` | A named bundle of platform permissions. | permission assignments, optional hierarchy, scope types, lifecycle | NIST Core and Hierarchical RBAC |
| `Permission` | Approval to perform an operation on a class of platform object. | operation, object/resource type, constraints | NIST RBAC permission = operation plus object |
| `RoleAssignment` | An effective-dated assignment of an access role to a platform user, membership, or team within a scope. | subject, role, scope, grantor/source, effective interval, revocation | NIST user-role assignment, extended with explicit tenant scope and history |
| `SeparationOfDutyConstraint` | A static or dynamic constraint on role assignment, activation, or a consequential workflow decision. | conflicting roles/actions, cardinality, scope, exception authority | NIST static and dynamic separation of duty |
| `SeparationOfDutyException` | A time-bounded, reason-required waiver of one separation-of-duty constraint for one record type in one tenant. | constraint, record type, reason, approving Org Admin, effective interval, revocation | Original product decision in M0-D03; NIST SoD supplies only the constraint concept |
| `TrustedIssuer` | An OIDC issuer that one tenant organization trusts for member sign-in. Several may be configured per organization. | tenant, exact issuer identifier, lifecycle, configuring actor | OpenID Connect issuer identifier; per-tenant trust is an original product decision in M0-A07 |
| `PlatformOperatorGrant` | An effective-dated grant of platform-operator authority to a platform user. Operator authority covers tenant lifecycle, administrator roster, and usage metadata only. | platform user, granting operator, effective interval, revocation, revoking operator | Original product decision in M0-D25 |
| `ServiceEngagement` | An advisory or attest service the firm provides to one client organization. It is distinct from the client's Type I or Type II audit engagement. | tenant, practice (`advisory` or `attest`), service type, scope, period, acceptance decision and approver, independence evaluation | Original product decision in M0-D25, M0-D26, and F1-07 |
| `EngagementAssignment` | An effective-dated assignment of a firm-staff platform user to one accepted service engagement, carrying that engagement's practice role. | service engagement, platform user, practice role (`Advisor` or `Attest`), effective interval, assigning actor, revocation | Original product decision in M0-D25 and M0-D26 |

Platform roles authorize actions in Compliance. They must not be reused for job
profiles, provider roles observed during an access review, or responsibilities
such as control owner and policy approver.

The first-release built-in `AccessRole` catalog (M0-D03) is Org Admin,
Compliance Lead, Contributor, and Viewer for client personnel, plus the
firm-staff practice roles Advisor and Attest. Stable role codes and permission
sets belong to the authorization decision (M0-A04, ADR 0002), not this catalog;
the existing `tenant_administration`, `compliance_management`, and
`compliance_participation` implementation roles are mapped there. A practice role is effective
only through an active `EngagementAssignment`; it is never granted as a
standing tenant `RoleAssignment`. Review, approval, and ownership duties are
`Responsibility` records held by members whose role permits the action; they
are not additional access roles. Custom roles are out of first-release scope.

### Applications, systems, and resources

| Entity | Canonical meaning | Important attributes and relationships | Standards alignment |
| --- | --- | --- | --- |
| `Application` | A logical software product or service the organization uses, develops, or supplies. | canonical name, description, delivery model, publisher/provider, business owner, lifecycle, criticality, classifications | IT/application portfolio concept; no broadly adopted interchange standard fully defines this inventory |
| `ClientService` | A client offering or operated service system included or excluded by a program boundary. It is not a software application or the firm's service engagement. | tenant, stable name, owner, purpose, lifecycle, boundary references | Original product decision in [M0-D22](https://github.com/bdgrz/compliance/issues/79) |
| `SystemInstance` | A concrete deployable, administrative, tenancy, account, subscription, or environment boundary of an application. This replaces ambiguous uses of `ReviewedSystem`. | application, environment, owning organization, operator/provider, region, source identifiers, lifecycle | Provider-neutral configuration-item pattern; SCIM service-provider boundary where applicable |
| `Resource` | An object or resource collection protected by a system instance. | system instance, type, parent resource, source identifiers, sensitivity, lifecycle | NIST RBAC object; provider-specific resource kinds remain extensions |
| `IntegrationEndpoint` | A configured connection through which the platform observes or manages a system instance. | system instance, connector type/version, authority, capabilities, health, credential reference | Operational integration record, not the system itself |

`Application` answers “what software or service is this?” `SystemInstance`
answers “which concrete boundary are we inventorying or reviewing?” An access
campaign targets one or more system instances; `reviewed` is a campaign or scope
state, not an entity type.

### Devices and infrastructure

| Entity | Canonical meaning | Important attributes and relationships | Standards alignment |
| --- | --- | --- | --- |
| `Device` | Independently managed physical equipment or appliance, such as a server, switch, router, firewall, endpoint, phone, printer, or storage appliance. | device kind, manufacturer, model, serial and asset identifiers, owner, custodian, location, lifecycle, management state | IETF hardware model for physical components; remaining inventory semantics are original product decisions |
| `DeviceComponent` | A physical component contained by a device. | device, component class, parent component, manufacturer, model, serial number, firmware, state | IETF RFC 8348 hardware component; product-defined class vocabulary |
| `ComputeInstance` | A logical compute environment such as a virtual machine, bare-metal OS instance, container host, or material serverless environment. | host, provider identifiers, environment, operating system, lifecycle, state | Original provider-neutral product decision |
| `NetworkInterface` | A physical or logical network attachment belonging to a device or compute instance. | parent, interface type, source identifiers, MAC addresses, administrative and operational state | IETF hardware model for physical interfaces; logical attachment semantics are original product decisions |
| `Network` | A governed logical network, segment, or zone. | environment, prefixes, classification, owner, lifecycle | Original product decision; provider-neutral inventory projection |
| `NetworkConnection` | A declared or observed topology relationship between interfaces or network endpoints. | typed endpoints, source, effective/observed interval, state | Relationship derived from network-management sources |
| `SoftwareInstallation` | An effective-dated relationship showing software or firmware installed on a device, component, or compute instance. | installation target, application or software release, version, source, observed/effective interval | Software inventory relationship; source-specific package identifiers remain extensions |
| `Location` | A physical site, hosting region, or other governed place relevant to the scoped service system. It is distinct from a device or provider. | tenant, type, name, geography or source reference, owner, lifecycle | Original product decision in [M0-D22](https://github.com/bdgrz/compliance/issues/79) |
| `EndpointClass` | A governed category of managed endpoints, such as company laptops or build agents, recorded instead of each individual device at first-boundary granularity. | tenant, name, owner, management baseline, platform/OS family, population source, lifecycle | Original product decision in M0-D08 |
| `OperationalProcess` | A governed system activity or data-handling process used in scope and system description; it is distinct from a written policy procedure. | tenant, purpose, owner, inputs/outputs, lifecycle, source | Original product decision in [M0-D22](https://github.com/bdgrz/compliance/issues/79) |

A physical server or switch is a `Device`; a virtual machine is a
`ComputeInstance`; software is an `Application`; and a concrete deployed
application boundary remains a `SystemInstance`. Hosting, installation, and
network topology are relationships among those independently identified
entities.

#### First-boundary granularity (M0-D08)

The first boundary and system description are inventoried at account and
environment level, not individual resource level:

| Inventory category | Canonical record |
| --- | --- |
| Cloud account, subscription, or project | `SystemInstance` of the cloud-platform `Application` |
| Environment (for example production or staging) | `SystemInstance` environment attribute, or its own `SystemInstance` when it is a separate administrative boundary |
| Network or VPC | `Network` |
| Data store | `SystemInstance` of the data-store `Application`, holding one or more `InformationAsset` records |
| Source repository | `Resource` of the source-control `SystemInstance` |
| Endpoint class | `EndpointClass`; individual `Device` records are optional |

Cloud consoles and device-management tools are discovery-only sources: their
observations propose records, and the governed inventory is authoritative.
Individual `Device`, `ComputeInstance`, and `NetworkInterface` records remain
valid but are not required for a first-boundary completeness claim.

### Information and providers

| Entity | Canonical meaning | Important attributes and relationships | Standards alignment |
| --- | --- | --- | --- |
| `InformationAsset` | A governed class or collection of information handled by the client service system, separate from a file used as evidence. | tenant, name, classification (`public`, `internal`, `confidential`, or `restricted`), owner, origin, permitted uses, retention reference, lifecycle | Original product decision; source-specific classifications remain observations |
| `Provider` | A vendor or subservice organization on which a client service depends. It is distinct from a source-system adapter and from the firm serving the client. | tenant, legal/display name, services supplied, owner, criticality, materiality, boundary treatment (subservice organizations default to `carve_out`), lifecycle | Original product decision; provider reports retain their own source wording and scope |
| `DataFlow` | A governed, versioned description of material information movement between typed system, process, provider, or location endpoints. It is distinct from a network connection or a file transfer observation. | tenant, exact version, source and destination at service-to-store granularity, information asset, purpose, protection expectation (encryption in transit and at rest), effective interval, boundary relevance | Original product decision for [R1-12](https://github.com/bdgrz/compliance/issues/50) |

### External identity and access

| Entity | Canonical meaning | Important attributes and relationships | Standards alignment |
| --- | --- | --- | --- |
| `Account` | A system-local identity record that may authenticate or receive access. | system instance, immutable source ID, username, account type, enabled state, lifecycle timestamps, optional subject correlation | SCIM User resource and common IAM account semantics |
| `ServiceIdentity` | A governed non-human subject such as a workload, service, automation, bot, or integration. | type, purpose, owning organization, accountable owner (`Person` or `Team`), environment, lifecycle, annual review date, expiry date | Original product distinction; provider workload/service-principal types map here when they represent the subject |
| `Group` | A source-system collection of accounts, groups, or other principals. | system instance, immutable source ID, display name, type, lifecycle | SCIM Group, including nested membership |
| `GroupMember` | One direct group-to-member relationship. | group, typed member reference, membership kind, source, effective/observed interval, lifecycle | SCIM Group `members`; nesting is explicit rather than flattened |
| `Role` | A source-system role: a named collection of entitlements or permissions. | system instance, source ID, role type, hierarchy, lifecycle | NIST RBAC role |
| `Entitlement` | A grantable access right exposed by a system, including a permission, license, group-membership right, permission set, or application role. | system instance, type, source ID, display name, privilege/sensitivity, resource scope | SCIM `entitlements` and `roles` as attributes; NIST RBAC permission where operation/object semantics are known |
| `RoleEntitlement` | A role-to-entitlement assignment. | role, entitlement, source, effective/observed interval | NIST permission-role assignment |
| `AccessAssignment` | A direct observed assignment of a group, role, or entitlement to an account or service identity, optionally scoped to a resource. | grantee, access item, resource, assignment mechanism, source, effective/observed interval | NIST user-role assignment and provider ACL/grant models |
| `EffectiveAccess` | A derived result explaining that a subject or account can exercise an entitlement over a resource. It is a snapshot/projection, not mutable source truth. | subject/account, entitlement, resource, derivation time, complete grant path, source snapshot | NIST authorization result; graph-derived extension needed for real provider data |

`Principal` may be used as a union type in APIs and relationship definitions,
not as a persisted catch-all entity. Its permitted variants should be explicit,
for example `Account | ServiceIdentity | Group` for a `GroupMember` relationship and
assignment. A provider “service principal” commonly produces both a source
`Account`-like object and a correlation to a governed `ServiceIdentity`.

`Group` and `GroupMember` are distinct entity types. `GroupMember` references
one group and one allowed member variant, normally `Account | Group` and,
where a source supports it, `ServiceIdentity`. Nested groups are direct
group-to-group relationships. `EffectiveGroupMember` is a derived transitive
relationship that retains the complete path. Whether `GroupMember` is later
placed inside a `Group` aggregate is deliberately unspecified here.

### Governance, observation, and history

| Entity | Canonical meaning | Important attributes and relationships | Standards alignment |
| --- | --- | --- | --- |
| `SourceSystem` | A named producer of source records. | provider, instance/tenant, authority by field or question | Supports W3C PROV agent/source attribution |
| `ExternalIdentifier` | A typed identifier assigned to a canonical entity by a source or namespace. | entity, source system, namespace, value, validity interval | SCIM `id` is service-provider-issued; `externalId` is client-issued |
| `Observation` | An immutable assertion made by a source about an entity or relationship at a time. | source, source record ID/version, observed time, payload identity, normalized assertions | W3C PROV Entity generated by an Activity and attributed to an Agent |
| `Correlation` | An attributable assertion that two records represent or relate to the same governed subject, with confidence and status. | left/right records, method, confidence, decision, actor, time | Explicit reconciliation extension; never inferred solely from email or display name |
| `Snapshot` | An immutable set of records and relationships used for a campaign, decision, or audit period. | definition, cutoff/as-of time, source completeness, content identity, rows, amendment chain | W3C PROV collection/derivation concepts |
| `IncidentReference` | An attributable reference to a source incident used for risk reassessment before the governed operating-period incident workflow exists. | tenant, source system and ID, occurrence time, summary, observation time, attribution | Original product decision in [M0-D22](https://github.com/bdgrz/compliance/issues/79) |
| `ControlRiskTreatment` | A reviewed assertion that an exact control version addresses a specific risk and treatment decision. | tenant, risk, control version, rationale, reviewer, effective interval | Original product decision in [M0-D22](https://github.com/bdgrz/compliance/issues/79) |

All records use both transaction and valid-time concepts where the distinction
matters:

- `effective_from` / `effective_to`: when the fact is true in the business or
  source system;
- `observed_at`: when Compliance learned it;
- `recorded_at`: when Compliance persisted it;
- `superseded_at`: when a governed successor replaced it.

## Relationship spine

### Identity and reference contract

This contract applies to every entity above. Each governed record has one
immutable, platform-generated ID and a required owning tenant ID; an
`Organization` used as a tenant owns its own realm. A global
`PlatformUser` and `FederatedIdentity` instead have a platform realm; their
tenant access exists only through `Membership`. A `SourceSystem` and its
observations are tenant-owned even when two tenants configure the same
provider. An external identifier is unique only within its source system,
namespace, and object type; it never grants access or proves a correlation.
References must resolve to live or historical records in the same tenant,
unless an explicitly named global identity reference is permitted. Missing,
unresolved, and redacted references are distinct states. An imported source
may report an unresolved target, but no governed relationship is asserted
until the target is resolved. Labels and contact values are mutable display
attributes and cannot be foreign keys.

For all tenant-owned governed records, `id`, `tenant_id`, `created_at`, and
lifecycle state are required. Creation and retirement retain actor, time, and
source. Optional fields in the tables above remain optional; a blank string is
not a substitute for a missing value. `active`, `inactive`, and `retired` are
common governed-record states, not a mandatory transition sequence. An
`Observation` is immutable; `unresolved` or `superseded` describes its
reconciliation, not a mutation of the observation. Retirement does not delete
identity, source attribution, or references captured in a historical decision.
Domain-specific lifecycles refine these states in the owning story; this
vocabulary does not imply a workflow transition or command boundary.

### Minimum entity fields

These are semantic requirements beyond the common identity, tenant/realm,
creation, and lifecycle fields above. They do not specify a request schema or
storage layout. A field listed only in an earlier "important attributes" cell
is optional unless a later feature contract makes it required. A required
source identifier is the immutable identifier supplied by that source, not a
display name or a provider-wide identifier guessed from another tenant.

| Entity | Additional required fields | Important optional fields |
| --- | --- | --- |
| `Organization` | organization kind, governed display name; a client tenant also requires legal name | parent, alternate IDs |
| `OrganizationalUnit` | organization, name, unit kind | parent, source IDs |
| `Person` | governed name or explicitly unknown-name state | contacts, locale, source IDs |
| `WorkRelationship` | person, organization, worker kind, effective start | end, employee number, manager, units, job profile |
| `JobProfile` | organization applicability, code or stable name | family, level, duties |
| `Responsibility` | typed assignee, responsibility kind, governed target, effective start | end, source, delegation |
| `FederatedIdentity` | exact issuer, subject, bound platform user | provider label, observed claims, person correlation |
| `PlatformUser` | security status | person correlation, additional federated identities |
| `Membership` | platform user, tenant organization, affiliation kind, effective start | invitation or provisioning source, end, suspension reason |
| `Team` | tenant organization, name, purpose | members, retirement reason |
| `AccessRole` | stable role code, scope types | display name, hierarchy, permissions |
| `Permission` | operation, platform object kind | constraints |
| `RoleAssignment` | typed subject, role, exact scope, effective start, grantor or source | end, revocation reason |
| `SeparationOfDutyConstraint` | constraint kind, conflicting actions or roles, scope | exception authority, limit |
| `SeparationOfDutyException` | constraint, record type, reason, approving Org Admin, effective start and end | revocation reason |
| `TrustedIssuer` | tenant organization, exact issuer identifier | display label |
| `PlatformOperatorGrant` | platform user, granting operator, effective start | end, revoking operator, revocation reason |
| `ServiceEngagement` | tenant organization, practice, service type, period start | scope, period end, acceptance decision, independence evaluation |
| `EngagementAssignment` | service engagement, platform user, practice role, assigning actor, effective start | end, revocation reason |
| `Application` | governed name, application kind | provider, owner, criticality, classifications |
| `ClientService` | governed name, purpose, owner reference | boundary references, retirement reason |
| `SystemInstance` | application, instance kind, stable governed name | environment, provider, region, source IDs |
| `Resource` | system instance, resource kind, stable local name or source ID | parent, sensitivity |
| `IntegrationEndpoint` | system instance, connector type and version, authority declaration | health, secret reference, capabilities |
| `Device` | device kind, governed name or asset identifier | manufacturer, serial, owner, location |
| `DeviceComponent` | device, component class, local component identifier | parent, serial, firmware, state |
| `ComputeInstance` | compute kind, governed name or source ID | host, provider IDs, OS, environment |
| `NetworkInterface` | typed parent, interface kind, local identifier | MAC, source IDs, administrative state |
| `Network` | governed name, network kind | environment, prefixes, classification, owner |
| `NetworkConnection` | two typed endpoints, declaration or observation kind, effective start or observed time | end, state, source |
| `SoftwareInstallation` | typed target, software reference, effective start or observed time | release, package IDs, end |
| `EndpointClass` | governed name, owner reference | management baseline, platform family, population source |
| `Location` | location kind, governed name | geography, provider region ID, owner |
| `OperationalProcess` | governed name, purpose, owner reference | inputs, outputs, source |
| `InformationAsset` | governed name, information kind, classification | owner, origin, uses, retention reference |
| `Provider` | legal or governed name, provider kind | services, owner, criticality, boundary treatment |
| `DataFlow` | stable flow ID and exact version, typed source and destination, information asset, purpose, effective start | end, protection expectation, scope decision, source |
| `Account` | system instance, source account ID, account kind | username, enabled state, subject correlation |
| `ServiceIdentity` | identity kind, purpose, accountable owner, review date | environment, expiry date |
| `Group` | system instance, source group ID, group kind | display name |
| `GroupMember` | group, typed member, source or governed decision, effective start or observed time | end, membership kind |
| `Role` | system instance, source role ID, role kind | hierarchy, display name |
| `Entitlement` | system instance, source entitlement ID, entitlement kind | display name, privilege, resource scope |
| `RoleEntitlement` | role, entitlement, source or governed decision, effective start or observed time | end |
| `AccessAssignment` | typed grantee, typed grantable, source or governed decision, effective start or observed time | resource, mechanism, end |
| `EffectiveAccess` | subject or account, entitlement, derivation time, grant path, completeness state | resource, source snapshot |
| `SourceSystem` | source kind, tenant-local name, authority declaration | provider, connection, source tenant ID |
| `ExternalIdentifier` | entity, source system, namespace, object kind, value, effective start | end |
| `Observation` | source system, source record ID, observed time, payload identity | source version, normalized assertions |
| `Correlation` | two typed records, method, decision status, actor or source, recorded time | confidence, effective interval |
| `Snapshot` | definition, cutoff, content identity, source completeness | rows, amendment parent |
| `IncidentReference` | source system, source incident ID, occurrence time or explicit unknown, observed time | summary, severity |
| `ControlRiskTreatment` | risk, exact control version, treatment kind, rationale, reviewer, effective start | end, review evidence |

These minimums allow a draft or unresolved inventory record to exist without
invented ownership or classification. An active record asserted to be in a
program or engagement scope must additionally satisfy its owning story's
scope-ready fields, or carry an explicit unresolved gap that prevents a
completeness claim. In particular, R1-10 requires application purpose, owner,
classification, concrete system instances, and access-review scope decision;
R1-12 requires owners, classifications, boundary relationships, and material
flow protection expectations. A draft's optional fields cannot be silently
treated as complete in a readiness or frozen-population calculation.

### Natural uniqueness

Every record's identity is its immutable platform ID. The natural keys below
are uniqueness constraints that detect duplicates; they are never references
or foreign keys. "Active" means the constraint applies to records whose
lifecycle is not retired and whose effective intervals overlap. A key scoped
"per tenant" is evaluated inside one tenant organization only.

| Entity | Natural uniqueness |
| --- | --- |
| `Organization` (tenant) | Platform realm: current and retired tenant slugs never collide or get reassigned; legal name is not unique |
| `OrganizationalUnit` | Per organization: unit kind plus name among active siblings of one parent |
| `Person` | None; duplicates are resolved by `Correlation`, never by name or email |
| `WorkRelationship` | Per organization: source-scoped employee number when present; otherwise none |
| `JobProfile` | Per organization: code or stable name among active profiles |
| `Responsibility` | Per tenant: assignee, responsibility kind, governed target, and overlapping interval |
| `FederatedIdentity` | Platform realm: exact `(issuer, subject)` |
| `PlatformUser` | None beyond platform ID |
| `Membership` | Per tenant: one active membership per platform user |
| `Team` | Per tenant: name among active teams |
| `AccessRole` | Platform realm for built-in roles: stable role code |
| `Permission` | Platform realm: operation plus platform object kind |
| `RoleAssignment` | Per tenant: subject, role, and exact scope with overlapping interval |
| `SeparationOfDutyConstraint` | Per tenant: constraint kind and scope |
| `SeparationOfDutyException` | Per tenant: constraint and record type with overlapping interval |
| `TrustedIssuer` | Per tenant: exact issuer identifier |
| `PlatformOperatorGrant` | Platform realm: one active grant per platform user |
| `ServiceEngagement` | None beyond platform ID |
| `EngagementAssignment` | Per engagement: one active assignment per platform user; per tenant, a user never holds overlapping `advisory` and `attest` assignments |
| `Application` | Per tenant: governed name among active applications |
| `ClientService` | Per tenant: governed name among active services |
| `SystemInstance` | Per application: governed name; per source: source identifier |
| `Resource` | `(system_instance_id, source_object_type, immutable_source_id)`, or local name under one parent |
| `IntegrationEndpoint` | None beyond platform ID |
| `Device` | Per tenant: asset identifier when present, else manufacturer plus serial |
| `DeviceComponent` | Per device: local component identifier |
| `ComputeInstance` | Per tenant: provider identifier when present |
| `NetworkInterface` | Per parent: local interface identifier |
| `Network` | Per tenant: governed name among active networks |
| `NetworkConnection` | Per tenant: endpoint pair, kind, and overlapping interval |
| `SoftwareInstallation` | Per target: software reference and release with overlapping interval |
| `EndpointClass` | Per tenant: governed name among active classes |
| `Location` | Per tenant: location kind plus governed name |
| `OperationalProcess` | Per tenant: governed name among active processes |
| `InformationAsset` | Per tenant: governed name among active assets |
| `Provider` | Per tenant: legal or governed name among active providers |
| `DataFlow` | Per tenant: stable flow ID plus exact version |
| `Account`, `Group`, `Role`, `Entitlement` | `(system_instance_id, source_object_type, immutable_source_id)` |
| `ServiceIdentity` | Per tenant: none; duplicates are resolved by `Correlation` |
| `GroupMember`, `RoleEntitlement`, `AccessAssignment` | Per source: both typed endpoints (plus resource for an assignment) with overlapping interval |
| `EffectiveAccess` | Per derivation: subject or account, entitlement, resource, and complete grant path |
| `SourceSystem` | Per tenant: tenant-local name |
| `ExternalIdentifier` | Per source system: namespace, object kind, and value with overlapping interval |
| `Observation` | Per source system: source record ID, source version, and payload identity |
| `Correlation` | Per tenant: the unordered record pair with an active decision |
| `Snapshot` | Per definition: content identity |
| `IncidentReference` | Per source system: source incident ID |
| `ControlRiskTreatment` | Per tenant: risk and exact control version with overlapping interval |

The accepted persistence decision,
[ADR 0003](../architecture/decisions/0003-event-sourced-history-and-effective-versions.md),
defines how these records, their revisions, and their effective history are
stored. This catalog constrains what is stored, not the storage layout.

### Relationship cardinality and time contract

The following are canonical relationship rules, not aggregate or storage
boundaries. `1` requires an existing endpoint at the relationship's effective
time; `0..1` and `0..*` allow absence. Where a relationship is observed from a
source, its direct assertion retains `source_system_id`, source record ID,
`observed_at`, and the source's effective interval when known. A governed
relationship also retains decision actor and `recorded_at`. Intervals are
half-open `[effective_from, effective_to)`; an absent end means open, not
infinite source certainty. Overlap is allowed only when the rows represent
distinct relationships or separately attributed, conflicting observations.

| Relationship | Endpoints and cardinality | Constraint |
| --- | --- | --- |
| Organization hierarchy | child `Organization` 0..1 parent; parent 0..* children | Same tenant realm; no cycles; legal independence is not inferred from hierarchy. |
| Organizational structure | `OrganizationalUnit` 1 organization, 0..1 parent unit; organization 0..* units | Parent unit belongs to the same organization; no cycles. |
| Workforce affiliation | `WorkRelationship` 1 `Person`, 1 `Organization`; each endpoint 0..* relationships | Parallel and successive relationships are distinct; employee number is source-scoped. |
| Workforce manager | relationship 1 subordinate and 1 manager `WorkRelationship`; each 0..* over history | Compatible organization and effective intervals; no self-management or effective cycles. |
| Job and unit assignment | `WorkRelationship` 0..1 `JobProfile`, 0..* units | Profile and units belong to an applicable organization; effective dates are retained. |
| Accountable work | `Responsibility` 1 assignee (`Person`, `Membership`, `Team`, or `OrganizationalUnit`), 1 governed object and scope | Assignee type, authority, and effective interval are explicit; a person need not sign in to be accountable. |
| Login binding | `FederatedIdentity` 1 `PlatformUser`; user 0..* identities | Exact issuer and subject are globally unique; rebinding requires an attributable governed decision. A second identity binds to an existing user only with proof of both identities; email never merges users. |
| Person correlation | `Person` 0..* identities, platform users, and accounts; each correlated identity/account 0..1 person | Correlation is explicit, revocable, and tenant-scoped; neither email nor name proves it. |
| Tenant membership | `Membership` 1 `PlatformUser`, 1 `Organization`; each endpoint 0..* over history | At most one active membership for the same user and organization; suspension revokes effective access immediately. |
| Team membership | `Team` 1 organization, 0..* memberships; member 1 `Membership` | Team and membership belong to the same tenant; membership intervals cannot outlive the tenant affiliation. |
| Engagement access | `EngagementAssignment` 1 `ServiceEngagement`, 1 `PlatformUser`; engagement 0..* assignments | Firm staff reach a tenant's business records only through an active assignment to an accepted engagement; ending the assignment revokes that access. One person never holds both an `advisory` and an `attest` practice assignment for the same client. |
| Operator authority | `PlatformOperatorGrant` 1 `PlatformUser`, 1 granting operator; user 0..* grants over history | Platform realm. Authorizes tenant lifecycle, administrator roster, and usage metadata only; it grants no tenant business-record access. |
| Tenant creation | Creating `Organization` 1 creating `PlatformUser` | Any signed-in platform user may create a tenant organization and receives its first `Membership` and Org Admin assignment (M0-D25). |
| Issuer trust | `TrustedIssuer` 1 `Organization`; organization 0..* issuers | Tenant sign-in trust, including any platform-default issuer, is defined by M0-A07; an identity never gains tenant access merely because its issuer is trusted. |
| SoD waiver | `SeparationOfDutyException` 1 `SeparationOfDutyConstraint`, 1 approving `Membership` | Time-bounded and reason-required; every exception is flagged in readiness and audit exports. |
| Platform role assignment | `RoleAssignment` 1 subject (`PlatformUser` or `Membership` or `Team`), 1 `AccessRole`, 1 explicit scope | Scope must be inside the authorized tenant; effective tenant access requires an active membership even when the subject is a global user. |
| Application deployment | `SystemInstance` 1 `Application`; application 0..* instances | Instance is tenant-owned; provider and operator are separately attributable references. |
| Integration connection | `IntegrationEndpoint` 1 `SystemInstance`, 1 connector type; instance 0..* endpoints | Credential reference names a secret location, never secret bytes; a connector's authority is explicit. |
| Service boundary | `ClientService` 0..* system instances, providers, locations, processes, and information assets | Inclusion is an effective-dated scope assertion, not ownership transfer; references stay tenant-local. |
| Provider dependency | `Provider` 0..* client services and system instances | Relationship records service supplied, scope treatment, source, and effective interval; a provider is never inferred from a connector. |
| Information movement | `DataFlow` 1 typed source, 1 typed destination, 1 `InformationAsset`; each endpoint and asset 0..* flows | Source and destination are tenant-local `Person`, `OperationalProcess`, `Application`, `SystemInstance`, `Provider`, or `Location` references; revision creates a new exact version. |
| Protected resource | `Resource` 1 `SystemInstance`, 0..1 parent `Resource`; instance 0..* resources | Parent belongs to the same instance; no cycles. |
| Physical containment | `DeviceComponent` 1 `Device`, 0..1 parent component; device 0..* components | Parent belongs to the same device; no cycles. |
| Compute hosting | `ComputeInstance` 0..1 host (`Device` or another `ComputeInstance`); host 0..* guests | Unknown host is explicit; no containment cycles. |
| Network attachment | `NetworkInterface` 1 parent (`Device` or `ComputeInstance`); parent 0..* interfaces | Exactly one parent type per interface at a time. |
| Network topology | `NetworkConnection` 2 typed endpoints (`NetworkInterface` or `Network`) | Endpoints are distinct and tenant-local; observed and declared connections remain separately attributed. |
| Software installation | `SoftwareInstallation` 1 target (`Device`, `DeviceComponent`, or `ComputeInstance`), 1 software `Application` | Release and package identifiers are versioned attributes with source attribution until a separate governed release catalog is approved. |
| External account | `Account` 1 `SystemInstance`; instance 0..* accounts; account 0..1 `Person` or `ServiceIdentity` correlation | Shared and unresolved accounts need no subject correlation. |
| Source group membership | `GroupMember` 1 `Group`, 1 member (`Account`, `Group`, or supported `ServiceIdentity`) | Member kind is explicit; nested cycles are retained as incomplete expansion, not flattened truth. |
| External role permission | `RoleEntitlement` 1 `Role`, 1 `Entitlement`; each endpoint 0..* edges | Both endpoints belong to the same system instance unless the source explicitly models a cross-instance grant. |
| Direct access grant | `AccessAssignment` 1 grantee (`Account` or `ServiceIdentity`), 1 grantable (`Group`, `Role`, or `Entitlement`), 0..1 `Resource` | Grant is direct and attributed; derived `EffectiveAccess` includes every contributing edge and a completeness status. |
| Source identity | `ExternalIdentifier` 1 canonical entity, 1 `SourceSystem`; entity 0..* identifiers | Source namespace, object type, and value are required; one tuple cannot identify two active entities in the same interval. Reuse over time is represented with separate intervals. |
| Source assertion | `Observation` 1 `SourceSystem`, 1 source record; source 0..* observations | Payload identity and observed time are required; no silent promotion to a governed fact. |
| Incident source reference | `IncidentReference` 1 `SourceSystem`, 1 source incident ID; source 0..* references | Occurrence and observation times are distinct; the reference is not an approved internal incident record. |
| Correlation decision | `Correlation` 2 typed records and 1 decision actor or source | Records may disagree; status and effective time preserve reversal history. |
| Frozen population | `Snapshot` 1 definition, 0..* immutable row references | Cutoff, content identity, completeness, and amendment chain are required before use in a decision. |
| Risk treatment | `ControlRiskTreatment` 1 risk, 1 exact control version | Review decision, rationale, and effective interval are retained; later control edits do not rewrite history. |

`SourceSystem` identifies one tenant-owned producer or manually governed
source; it has 0..* observations and identifiers. `SourceSystem` authority is
declared by field or question and can change over time without rewriting prior
observations. An `AccessRole` has 0..* permissions, and a `Permission` may
appear in 0..* roles through attributable assignments. Neither a role nor a
permission grants tenant access without an effective `RoleAssignment`.
`OperationalProcess`,
`InformationAsset`, and `Location` are independent tenant-owned records;
their inclusion in a client service or program is a separate, effective-dated
scope relationship. No arbitrary UUID is a valid reference.

```text
Person --< WorkRelationship >-- Organization --< OrganizationalUnit
   |
   +--? FederatedIdentity >-- PlatformUser --< Membership >-- Organization
   |                                  |
   |                                  +--< RoleAssignment >-- AccessRole --< Permission
   |
   +--? correlation -- Account >-- SystemInstance >-- Application
                              |             |
ServiceIdentity --?-----------+             +--< Resource
                              +--< GroupMember >-- Group
                              +--< AccessAssignment >-- Role / Entitlement / Group
                                                        |
                                                        +-- EffectiveAccess (derived path)
```

The `?` edges are optional correlations, not ownership. A person can exist with
no login or external account. An account may be shared, unresolved, or non-human
and therefore have no person correlation. A platform user can participate in
several tenants, while workforce relationships remain organization-specific.

## Required invariants

- `FederatedIdentity` is unique by exact `(issuer, subject)`.
- `Account`, `Group`, `Role`, `Entitlement`, and `Resource` are unique by
  `(system_instance_id, source_object_type, immutable_source_id)`.
- A `Membership` references exactly one platform user and one tenant
  organization; disabling a workforce relationship does not silently mutate it.
- A `WorkRelationship` references one person and one organization and may not
  use email as either identity.
- A manager relationship connects workforce relationships valid in compatible
  organization and time scopes; it does not make “manager” a person type.
- A group may contain only the principal variants supported by its source.
  Cycles and incomplete nested expansion are recorded, not discarded.
- A role contains entitlements; a group contains principals. If a provider uses
  one object for both, the adapter emits both facets linked to the same source
  object rather than weakening the canonical meanings.
- Direct assignment and inherited/effective access are never stored as the same
  fact. Every effective-access row retains all contributing edges.
- Deletion or absence in a source import becomes a tombstone or reconciliation
  state until source completeness and lifecycle policy authorize retirement.
- Historical decisions retain stable entity IDs plus display snapshots. Later
  renames, correlations, or deprovisioning do not rewrite attribution.

## Mapping approved source shapes

This section is a non-normative note; the normalized entities, uniqueness,
relationships, and time rules above are the contract. The table maps only
source shapes whose semantic references are in the approved-source register,
plus explicitly original product inputs. Import and connector design are later
delivery work; a connector still needs its own source-version,
rights, field, completeness, and failure review before ingesting data. Every
mapped row keeps the exact source system, record ID, version when available,
payload identity, observed time, and mapping decision. Unknown source fields
remain in the attributable observation and do not silently create canonical
facts. An unavailable target is an unresolved reference, not a fabricated
entity.

| Source term | Canonical mapping | Notes |
| --- | --- | --- |
| SCIM `User` | `Account`; optionally correlate to `Person` or `ServiceIdentity` | SCIM describes a service-provider user resource, not the universal person record |
| SCIM EnterpriseUser | `WorkRelationship` plus organization-unit relationships | Preserve the original SCIM resource and manager reference as observation provenance |
| SCIM `Group` | `Group` plus direct `GroupMember` relationships | Do not flatten nested groups on ingestion |
| OIDC ID Token subject | `FederatedIdentity` | Key by exact issuer and subject; claims are observed attributes |
| Product-authored application catalog entry | `Application`; an explicitly identified deployment becomes a `SystemInstance` | This is an original product input, not a vendor schema mapping |
| Product-authored workforce entry | `Person` plus `WorkRelationship` | This is an original product input; any later HRIS mapping needs exact source review |
| Compliance control owner | `Responsibility` | Original product input; it is neither a job profile nor an access role |

Provider-specific Entra, AWS, GitHub, HRIS, and other adapter mappings are
deferred until their exact public source versions and intended use pass the
source policy. Provider names and sample field semantics are not normative for
this catalog.

## Extension policy

The canonical core changes only when a concept has stable cross-provider
semantics and at least two real consumers. Otherwise use one of these extension
points:

1. source payload retained immutably on the `Observation`;
2. namespaced source attributes, such as `entra:accountEnabled`;
3. a typed provider facet linked to a canonical entity;
4. a governed classification or tag whose vocabulary and owner are explicit.

Extensions must not redefine canonical IDs, overload lifecycle state, turn a
derived fact into source truth, or bypass tenant and provenance rules.

## Adoption in the existing domain model

Use these replacements and clarifications when resolving M0-D22:

| Existing term | Canonical treatment |
| --- | --- |
| `Person` | Keep; add `WorkRelationship` rather than employment fields directly on the person |
| human `AccessSubject` | Remove as a duplicate entity; use `Person` correlated to one or more `Account` records |
| NHI `AccessSubject` | Rename to `ServiceIdentity` |
| `DirectoryPrincipal` with kind account | `Account` |
| `DirectoryPrincipal` with kind group | `Group` |
| `DirectoryPrincipal` with kind role | `Role` |
| `DirectoryPrincipal` with workload/service kind | Source account facet correlated to `ServiceIdentity` |
| `ReviewedSystem` | Rename to `SystemInstance`; review inclusion belongs to scope/campaign records |
| `ExternalAccessGrant` | Rename to `AccessAssignment` for observed direct edges; use `EffectiveAccess` for derived paths |
| `Member` | Rename to `Membership` to make its relationship semantics explicit |
| `Team` | Keep as a platform-managed collection, separate from external `Group` |
| `AccessRole` | Keep for Compliance authorization, separate from external `Role` |
| `Application` | Keep; define it as the logical product/service above its system instances |
| `ExternalIdentity`, `UserIdentity` | `FederatedIdentity` |
| `User` | `PlatformUser` |
| `AccessGrant` | `RoleAssignment` for tenant roles; `EngagementAssignment` for firm-staff practice access |
| reviewer, management approver, and read-only advisor access roles | Retired from the role catalog; review and approval are `Responsibility` records, and advisory access is the Advisor practice role through `EngagementAssignment` |

Migration should be semantic before it is physical: update the glossary and
acceptance criteria first, then introduce persistence/API representations. Do
not rename existing data structures until adapters and history-preserving data
migrations are defined.

## Approval record

Approved 2026-09-22 by Jeff Repanich (product owner and tech lead) under
[M0-D28 #139](https://github.com/bdgrz/compliance/issues/139).

**Decision.** This catalog is the canonical entity and relationship vocabulary
for the first release. Backlog issues, domain slices, acceptance criteria, and
implementation subtasks use these entity names and relationship rules. A story
that needs a concept missing from this catalog adds it here, in the same review
as the story, and labels it as either a registered-source alignment or an
original product decision.

**Rationale.** The catalog separates the person, the workforce relationship,
the sign-in identity, the platform user, and each external account, and it
separates applications from their system instances and groups from roles.
Without those separations an access review cannot explain actual versus
expected human and non-human access. Composing registered public standards
keeps identity and access semantics interoperable. Labeling everything else as
an original product decision prevents an implied standards-conformance claim.

**2026-09-22 decisions incorporated.** Tenancy and operator authority
(M0-D25), roles, separation-of-duty exceptions, and non-signing responsibility
holders (M0-D03), advisory and attest engagement assignment (M0-D25, M0-D26),
per-tenant trusted issuers and identity linking (M0-A07), workforce source
authority (M0-D06), first-boundary inventory granularity and classification
(M0-D08), and provider materiality and carve-out default (M0-D11).

**Source and use approval.** Every standards alignment in this catalog cites
a source in the approved reference register of the
[source-reference policy](source-reference-policy.md), with its exact version
and use terms, and every approved use is `reference`. No source material is
copied. Every other entity and relationship is labeled an original product
decision. DMTF CIM, DMTF Redfish, and NIST SP 800-162 update 2 remain in the
excluded-source register and contribute no names, definitions, fields, or
mappings.

**Coverage of blocked stories.**

| Story | Catalog sections it relies on |
| --- | --- |
| R1-15 (#127, #152, #176), R1-15a (#237) | `Organization` as tenant, `Membership`, `PlatformOperatorGrant`, tenant creation, `TrustedIssuer` |
| EN-01 (#87, #157) | `PlatformUser`, `Membership`, `AccessRole`, `Permission`, `RoleAssignment`, `EngagementAssignment`, separation of duty, identity and reference contract |
| R1-04 (#10), R1-04a (#93, #183) | `FederatedIdentity`, login binding, `Membership`, `Team`, `RoleAssignment`, `Responsibility`, `SeparationOfDutyException` |
| R1-02 frontend (#178) | `ClientService`, service boundary, `SystemInstance`, `Provider`, `Location`, `OperationalProcess`, `InformationAsset` |
| R1-10 (#48), R1-10a (#102, #211) | `Application`, `SystemInstance`, `ExternalIdentifier`, application deployment |
| R1-11 (#49), R1-11a (#106, #219) | `Person`, `WorkRelationship`, workforce manager, `ServiceIdentity`, workforce source authority |
| R1-12 (#50, #227) | Devices and infrastructure, first-boundary granularity, `InformationAsset`, `DataFlow`, `Provider` |
| EN-05 (#91, #195) | `SourceSystem`, `Observation`, `ExternalIdentifier`, `Correlation`, source assertion; the import workflow itself is outside this catalog |
| R2-06 (#20), R2-06a (#114, #273) | External identity and access, `EffectiveAccess`, `Snapshot`, frozen population |

**Explicitly deferred.** Aggregate roots, transaction and consistency
boundaries, persistence layout, API resource shapes, and service boundaries
are not decided here. The accepted architecture decisions and each story's
delivery slice decide them. Stable role codes and permission sets belong to
M0-A04. Provider-specific adapter mappings (Entra, AWS, GitHub, named HRIS
vendors) each need their own source-version and rights review before a
connector ingests data. They are delivery work in the consuming story and do
not reopen this catalog.

## Standards references

- [RFC 7643: SCIM Core Schema](https://www.rfc-editor.org/rfc/rfc7643)
- [RFC 7644: SCIM Protocol](https://www.rfc-editor.org/rfc/rfc7644)
- [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html)
- [NIST IR 6192: A Revised Model for Role Based Access Control, July 1998](https://csrc.nist.gov/pubs/ir/6192/final)
- [W3C PROV-O Recommendation, April 2013](https://www.w3.org/TR/2013/REC-prov-o-20130430/)
- [RFC 8348: A YANG Data Model for Hardware Management](https://www.rfc-editor.org/rfc/rfc8348)
NIST SP 800-162 update 2 is excluded by the source register pending clearance
of its mixed-author material. DMTF Redfish is also excluded pending patent and
intended-use clearance; no Redfish schema or semantics shape this catalog.
Product-specific entities and relationships in
this catalog remain original decisions unless an exact mapping is stated.
