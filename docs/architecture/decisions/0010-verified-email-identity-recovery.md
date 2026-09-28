# Verified-email identity recovery

Status: accepted for R1-04a backend, 2026-09-28. Decision owner: Jeff
Repanich (product owner and tech lead).

[ADR 0009](0009-tenant-identity-federation-and-context.md) makes a provider
identity the immutable issuer plus subject pair and requires explicit proof to
link identities. This record defines the lost-identity recovery rules left
open there.

## Decision

- Recovery requires two independent proofs: control of an email address
  previously verified and owned by the platform user, and fresh authentication
  from the replacement OIDC issuer and subject. A matching mutable email claim
  from an OIDC provider is not sufficient.
- The platform user ID stays stable. Recovery preserves membership, roles,
  authorship, and assignments attached to that user. The chosen old provider
  identity is revoked only after the replacement is registered to the same
  user.
- An anonymous start request returns the same success response for unknown,
  unverified, and verified addresses. A verified address can have one active
  challenge at a time. The challenge is a purpose-separated 256-bit HMAC token,
  stored only as a hash, and expires after 15 minutes. Repeated starts during
  that window do not send another challenge.
- The email proof claims a challenge for one old and replacement identity pair.
  The durable claim records that email proof, so the exact pair can resume after
  challenge expiry if a later write failed. Attempting to retarget the challenge
  conflicts, and every resume still requires fresh authentication as the bound
  replacement identity and same-user ownership checks.
  The completion and notice are attributed to the authenticated replacement
  identity. Completion notices retry independently from challenge delivery.
- Identity registration, revocation, and recovery completion are separate
  aggregate writes. Each step is idempotent for the claimed pair and can be
  resumed after partial failure. The old identity is revoked as soon as the
  replacement is successfully registered; this also invalidates browser
  sessions bound to the old identity.
- Browser session tokens carry the provider identity ID. Every authenticated
  request checks that identity against authoritative aggregate state and fails
  closed when it is missing, revoked, mismatched, or unavailable. Existing
  sessions without the identity binding must sign in again after deployment.
- The identity directory is a derived lookup of active identities by platform
  user. Options reads require a caught-up projector; lag returns a transient
  conflict. The response carries the directory's monotonically increasing
  numeric `projection_revision`, and callers may supply a minimum revision.
  The projector increments once for every committed source event, including
  events that do not change visible rows. It commits that counter with the
  projection rows and Portia checkpoint. The checkpoint cursor remains an
  opaque replay token.

## Abuse controls and operations

The aggregate cooldown limits challenge delivery to one message per verified
address per 15-minute window and prevents address enumeration through the
response. It does not limit attempts from one caller across many addresses.
Deployment ingress should apply caller/IP throttling to anonymous recovery
starts. The application does not infer client IP from forwarded headers until
the trusted proxy chain is configured; a process-local limiter behind an
unknown load balancer can throttle every customer together or trust spoofed
addresses.

Recovery HMAC keys are shared by API and worker through secret configuration.
Keep each old key available until every challenge issued under it has expired.
SMTP delivery can retry after an unacknowledged send and does not guarantee
exactly-once delivery.

## Consequences

Recovery is a personal HTTP-only flow; it is not an MCP tool. A successful
recovery keeps the user's authorization and historical attribution intact while
ending the old provider identity's sessions. Discovery and identity lookup
remain projection concerns; aggregate streams own recovery proof, identity
ownership, and revocation invariants.
