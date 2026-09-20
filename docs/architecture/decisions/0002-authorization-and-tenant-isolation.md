# Authorization and tenant isolation

Status: proposed for M0-A04 review. The R1-15, EN-01, and R1-01 implementation
in PR #159 proves the first consuming paths; field-level restrictions and firm
independence still require their owning decisions and features.

Decision owner: product owner and tech lead. Review with the security lead before
marking M0-A04 accepted.

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
  specific permission is denied. Platform-operator operations use an explicit
  configured user-id authority and do not imply tenant membership.
- Query projections, including list and count results, use a tenant realm in
  Fitz. New search, artifact, export, notification, and job paths must preserve
  the same tenant scope and run cross-tenant tests before their backend children
  close. Restricted rows must be filtered before pagination and counting.
- Policies are in code and registered through Portia's source-generated request
  pipeline for Native AOT compatibility. Every business `/api/v1` endpoint
  declares authorization metadata; a host-level architecture test checks the
  mapped endpoints. The specific request authorizer remains the source of the
  row-level decision.
- Consequential events retain a stable actor identifier and display snapshot.
  The display comes from the same authenticated Bdgrz session identity as the
  member subject; an external provider claim on a combined principal cannot
  supply the event's actor display.
  A later identity rename or provider replacement must not rewrite historical
  authorship. System processes use a distinguishable actor, and personal
  acknowledgements, attestations, approvals, and sign-offs require a human
  HTTP session rather than an MCP tool.

## Alternatives considered

- A mutable server-side active-tenant session would make requests depend on
  browser tab state. Explicit tenant paths make concurrent clients and worker
  messages independently scoped.
- Provider token roles would delay revocation and couple client grants to
  federation. Platform membership and grants remain authoritative.
- Filtering only in the browser would expose other-tenant records through API
  lists, counts, and tools. Authorization and projection scoping occur on the
  server.

## Evidence and remaining decisions

PR #159 includes an explicit endpoint-policy test, nonmember versus missing
tenant responses, a two-tenant program-list isolation test, and standalone and
split API/worker broker flows. Earlier R1-15 PRs #151 and #153 through #156
cover provisioning, invitation, suspension, slug history, and worker recovery.

M0-D25 must confirm firm-staff access and ownership of firm-authored material.
M0-D26 must settle advisory/attest independence compartments before those
grants are implemented. Workforce and evidence stories must define sensitive
field classes and field-level redaction. Their search, artifacts, exports,
notifications, and background jobs will supply further isolation tests as
those paths are delivered. Denied-action telemetry needs a concrete logging
contract that does not leak client data.
