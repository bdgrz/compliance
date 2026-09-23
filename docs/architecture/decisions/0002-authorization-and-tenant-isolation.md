# Authorization and tenant isolation

Status: accepted for M0-A04, 2026-09-22. Decision owner: Jeff Repanich (product
owner and tech lead). The R1-15, EN-01, and R1-01 implementation in PR #159 and
the fail-closed composition in PR #325 prove the evaluation model; this record
adds the denied-action logging contract and the product rules below. Feature
stories implement their own grants against this model and bring their own
cross-tenant evidence.

## Decision

- The authenticated platform user is an identity, not an access grant. Every
  organization-scoped HTTP or MCP operation carries an opaque `tenant_id` in its
  contract. The request selects exactly one tenant; no process-wide active
  tenant or identity-provider membership claim grants access. Background work
  must retain the tenant stream realm and a named system actor.
- Portia request authorizers evaluate membership, event-sourced tenant activity,
  and the permission needed by the command or query before a handler reads or
  writes business data. An unknown tenant and a tenant without membership both
  return not found. A member of a suspended tenant or a member lacking a
  specific permission is denied. One platform user may hold memberships in
  several organizations; each request is evaluated only against the membership
  for its own `tenant_id`, so access to one organization never widens another.
- Query projections, including list and count results, use a tenant realm in
  Fitz. New search, artifact, export, notification, and job paths must preserve
  the same tenant scope and run cross-tenant tests before their backend children
  close. Restricted rows must be filtered before pagination and counting.
- Policies are in code and registered through Portia's source-generated request
  pipeline for Native AOT compatibility; no external policy engine is used.
  Every business `/api/v1` endpoint declares authorization metadata; a
  host-level architecture test checks the mapped endpoints. The specific request
  authorizer remains the source of the row-level decision. The shared
  Compliance composition enables Portia's fail-closed `RequireAuthorization()`
  check: hosted startup and direct dispatch reject a registered request that has
  no applicable authorizer or declared permission. Developer identity
  continuation has a Portia authorizer that permits it only when developer
  authentication is enabled, and it has no MCP tool. OIDC continuation has a
  Portia authorizer that requires an authenticated external issuer and subject.
  Tenant slug and owner lifecycle commands carry a narrow marker and accept only
  Portia's trusted system actor, which the lifecycle reactors use; they are not
  HTTP or MCP operations.
- Consequential events retain a stable actor identifier and display snapshot.
  The display comes from the same authenticated Bdgrz session identity as the
  member subject; an external provider claim on a combined principal cannot
  supply the event's actor display.
  A later identity rename or provider replacement must not rewrite historical
  authorship. System processes use a distinguishable actor, and personal
  acknowledgements, attestations, approvals, and sign-offs require a human
  HTTP session rather than an MCP tool.

## Product rules (2026-09-22)

These rules were decided by the product owner. Each is implemented by the
feature that owns it; until that feature ships, the current narrower behavior
stays in force and nothing is granted implicitly.

| Rule | Decision | Implementing records |
| --- | --- | --- |
| Client role catalog | Built-in client roles are **Org Admin**, **Compliance Lead**, **Contributor**, and **Viewer**. Today's built-ins map as Tenant Administration → Org Admin, Compliance Management → Compliance Lead, Compliance Participation → Contributor; Viewer is added with `tenant.access` only. Contributors write only records they own or are assigned. | R1-04b #186 (with R1-04a #183) |
| Firm staff | Firm staff hold **Advisor** or **Attest** roles, granted only through assignment to an accepted engagement for that client. There is no standing firm-staff membership. Removing the assignment revokes access on the next request; the grant is evaluated per request, not cached in a token. | F1-07 #269 |
| Independence compartments (M0-D26 strict wall) | One person can never hold both an Advisory and an Attest assignment for the same client. Attest assignees cannot read that client's advisory working-note records. An attest engagement cannot be accepted for a client that received control design, implementation, or operation from the firm within the 12 months before acceptance, including ongoing work; the look-back reads engagement history. A readiness assessment alone in that window is conditionally compatible and requires a documented partner evaluation recorded on acceptance. `IndependenceCompartments` implements these rules in code; engagement assignment, record authorizers, and engagement acceptance call it once engagement records exist. | F1-07 #269, F1-08 #277, M0-D26 #124 |
| Platform operator | Operators see tenant metadata only: lifecycle, administrator roster, and usage. They get no business-record access unless they also hold a membership or engagement assignment, which the ordinary authorizers evaluate. Operator status is granted and revoked in the product by an existing operator, with an attributable audit event; the configured operator list remains only as the bootstrap for the first operator. | R1-15 backend #152 |
| Separation of duties | Self-review and self-approval are blocked by default. An Org Admin may record a time-bound, reason-required separation-of-duties exception per record type; each exception is attributable, expires, and is flagged in readiness and audit exports. | EN-04 backend #161, R1-04e #192 |
| Offline responsibility holders | A responsibility may be held by a workforce person who never signs in. A signed-in member records that person's work on their behalf; the event carries both the recording member as actor and the responsible person, and neither replaces the other. | EN-01 backend #157, R1-11a #219 |
| Imports | Accepting or canceling an import requires Compliance Lead or Org Admin. Contributors may stage and preview. | EN-05 backend #195 |

## Field-level restrictions

Workforce and evidence records can carry fields more sensitive than the record
itself. The mechanism is fixed now; the classified fields are not:

- A record contract names each restricted field's **field class** (for example
  `workforce.sensitive` or `evidence.restricted`). Each class maps to a read
  permission `field.<class>.read` evaluated by the same in-code permission
  authorizer for the request's tenant and member.
- Handlers that return, list, search, or export such records redact every field
  whose class the actor lacks before pagination, counting, or serialization.
  Redaction produces an explicit `redacted` marker, never an empty or default
  value that could be mistaken for data. Filters and sort keys over a restricted
  field are refused for actors without its class.
- `FieldClasses` and `FieldRestrictions` are the hook. The first class is
  `evidence.quarantined_content`: evidence content held in quarantine is
  readable only by Org Admin (M0-D16). The permission is granted to the Org Admin
  role when the first evidence-quarantine consumer ships, not before.
- Sensitive workforce classes follow M0-D06 #63; the expected set is personal
  contact details, employment-status reason, and manager chain. R1-11d #225 and
  R2-12 #285 implement the first consumers and their redaction tests; no field
  is restricted before then.

## Denied-action logging

Every denial made by a Portia request authorizer or declared permission while
an API host serves an HTTP or MCP call writes one structured warning:

- Category `Bdgrz.Compliance.Authorization`, event `AuthorizationDenied`
  (id 4031).
- Fields: `outcome` (`unauthorized`, `forbidden`, or `not_found`), `stage`,
  `component` (the authorizer or `permission`), `transport` (`http` or `mcp`),
  `method`, `route` (the route template, never its values), `tenant_id` (only
  when the route carries a valid opaque id), `actor_id` (the Bdgrz subject), and
  `trace_id`.
- It never records emails, names, slugs, query strings, or bodies. A not-found
  denial is logged the same way as a forbidden one, so operators can see
  cross-tenant probing while the caller still receives the indistinguishable
  not-found response.

`AuthorizationDenialLog` subscribes to Portia's versioned
`portia.authorization.duration` instrument, which Portia records synchronously
on the dispatching call for each authorizer and permission check, and reads the
ambient request for the opaque identifiers above. Because the listener is
process-wide, a host only records calls whose request services belong to it.
Authentication failures rejected by ASP.NET Core before dispatch (no session)
are outside this record and remain the authentication middleware's logging.
MCP routes carry the tenant in tool arguments, so their records omit
`tenant_id`. Background dispatch runs as the named system actor and is not
denied in normal operation; a failure there surfaces through Portia's runner
fault logging. Retention of these records follows the platform log pipeline.

A native Portia authorization-outcome callback carrying the request context
would remove the dependency on the telemetry instrument; if Portia adds one,
this listener is replaced without changing the record contract.

## Alternatives considered

- A mutable server-side active-tenant session would make requests depend on
  browser tab state. Explicit tenant paths make concurrent clients and worker
  messages independently scoped.
- Provider token roles would delay revocation and couple client grants to
  federation. Platform membership and grants remain authoritative.
- Filtering only in the browser would expose other-tenant records through API
  lists, counts, and tools. Authorization and projection scoping occur on the
  server.
- An external policy engine (OPA, Cedar) would add a runtime and reflection or
  interpreter dependencies that conflict with Native AOT, and would split the
  row-level decision from the event-sourced membership state it depends on.
- Logging denials inside each authorizer would be inconsistent and easy to omit.
  Logging from HTTP status codes cannot tell an authorization not-found from a
  genuinely missing record. The Portia instrument reports exactly the
  authorization outcomes, once per policy.
- Standing firm-staff membership would make independence walls and revocation
  depend on remembering to remove memberships. Engagement assignment makes the
  grant expire with the engagement.

## Consequences

- New feature slices must declare a permission or authorizer (enforced at
  startup), keep projections tenant-realm scoped, and add a cross-tenant test for
  every new list, count, search, artifact, export, notification, or job path.
- Role catalog, operator grants, engagement assignment, compartments,
  separation-of-duties exceptions, on-behalf-of attribution, and field classes
  are feature work in the records named above; they must use this model rather
  than a feature-local check.
- Security operations can alert on `AuthorizationDenied` records without
  handling client data. Alert on `forbidden` and `unauthorized` volume per
  actor, and on `not_found` denials that one actor accumulates across several
  distinct `tenant_id` values (probing). A brief run of `not_found` for one
  actor in one tenant is expected while a new membership projects, so it is not
  an alert condition by itself.
- The denial listener never changes an authorization result: an exception
  while observing or writing the record is swallowed.

## Evidence

PR #159 includes an explicit endpoint-policy test, nonmember versus missing
tenant responses, a two-tenant program-list isolation test, and standalone and
split API/worker broker flows. Earlier R1-15 PRs #151 and #153 through #156
cover provisioning, invitation, suspension, slug history, and worker recovery.
PR #325 adds fail-closed composition. PRs #288 and #317 normalize
tenant-scoped paged reads. `AuthorizationDenialLogE2ETests` proves that an
outsider's cross-tenant read writes exactly one denial record with only opaque
identifiers, in both the standalone host and the split API host with an
independent worker. `IndependenceCompartmentsTests` and `FieldRestrictionsTests`
prove the in-code independence and field-redaction rules; they read no host
state, so they behave the same in both host modes.
