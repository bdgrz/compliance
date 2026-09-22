# Client tenancy and operator provisioning

Status: implementation decision for the R1-15 API/MCP slice, 2026-09-19.
Decision owner: product owner and tech lead; acceptance remains tracked in M0-D25 and M0-A07.

> [!NOTE]
> Superseded in part by [ADR 0009](0009-tenant-identity-federation-and-context.md),
> accepted for M0-A07 on 2026-09-22. It replaces the identity-broker preference
> with directly validated multiple trusted issuers, accepts the tenant-context
> contract below, and records that organization creation becomes self-service
> and operator status becomes an in-product grant. R1-15 must adopt those
> changes. Until it does, the provisioning mechanics below describe the current
> implementation.

## Decision

- A client organization is the only tenant boundary. `tenant_id` is its immutable, opaque UUID in APIs, events, jobs, and storage realms; `Organization` is the product name. Every client business record belongs to exactly one tenant. Platform-level criteria editions, methodology templates, and the firm-staff directory may exist outside tenants but cannot contain client records.
- Platform operators are platform users whose UUIDs are explicitly configured in `PlatformOperators:UserIds`. No production operator is inferred from authentication claims, email domains, or tenant membership. Developer identities are operators only in the local developer-authentication mode.
- A platform operator is the platform super administrator for platform operations: organization provisioning, suspension, reactivation, slug administration, invitation of the first administrator and firm staff, and inspection of organization metadata and memberships. The operator portfolio is a paginated platform-scoped query at `GET /api/v1/platform/tenants` and a read-only MCP tool. It includes provisioning and suspended organizations. The current implementation does not automatically grant tenant membership or client business-record access; cross-tenant client-data authority remains a pending M0-D25 and M0-D03 decision.
- Production registration records display name, legal name, slug, and operator attribution. It creates a provisioning tenant and an email invitation for the first client administrator. The operator receives no implicit membership on this path. Acceptance requires the invited user to own and verify the email address, then grants an explicit `client_personnel` membership and the administrator team. Only then does the tenant become active. Legacy developer-mode registration without a first administrator still bootstraps its creator for local compatibility. Invitation delivery is mocked until a real email adapter is supplied.
- A firm-staff invitation creates a `firm_staff` membership when accepted. Membership alone grants no standing access; a later service-engagement assignment must grant appropriate access. Advisory or attest practice designation belongs in a future platform-level staff record, not in the tenant membership.
- Suspension preserves events, projections, and memberships while tenant-scoped authorization reads the event-sourced current lifecycle and denies immediately. Reactivation restores access without recreating records. System reactions may finish cleanup and bootstrap while suspended.
- Browser selection is derived from the signed-in user's membership list. There is no server-side mutable “active tenant” claim or token role. Every tenant API path carries `tenant_id`, which the server authorizes. The browser may retain one selected tenant in navigation state; changing selection must clear tenant-specific client state when UI work begins.
- Browser slugs are attributes. The server normalizes and validates them against a single reserved-route registry. New slugs are reserved in an event-sourced slug aggregate before a tenant switches. Old slugs retain their owner, become permanently unavailable for new registrations, and resolve only for an authenticated member of that active tenant. Unknown and inaccessible slugs return the same not-found response.
- Slugs can reveal a client name in browser history and server logs. The API host sends `Referrer-Policy: no-referrer` on browser routes, static assets, and API responses so navigation does not forward a client path as a referrer. Product naming guidance and log retention still belong to M0-A07.
- API startup checks each newly reserved top-level route against event-sourced slug ownership, including retired slugs, and fails if an organization claimed it before the route was added. Registration cannot claim a reserved route even through a direct system command. Resolve a collision before deploying the new route.
- One platform user is bound to an exact issuer-plus-subject identity and can hold several memberships. A future multi-organization identity broker with per-organization connections is preferred over trusting arbitrary issuers in each tenant request. The current single-authority login remains the initial deployment configuration; issuer federation and identity linking require a separate security review before more authorities are enabled.

## M0-A07 tenant-context contract

The URL's opaque `tenant_id` selects the tenant for an organization-scoped HTTP
operation. Portia's route binding wins if JSON also includes `tenant_id`; the
handler, authorizer, and stream use the route value. The two-tenant program
broker test supplies a conflicting body value and verifies that only the URL
tenant changes. API clients should omit the redundant body field. MCP tools
carry one explicit tenant ID in their typed input and run the same request
authorizer. The server never accepts an IdP organization or role claim as a
membership grant.

Fitz tenant-owned streams use the immutable tenant UUID as their realm. A
reactor keeps that realm, its triggering event, and a named system actor when
dispatching a follow-up request. Projector reads and checkpoints use a tenant
realm. New cache keys, search indexes, artifact paths, exports, jobs, and
notifications must include the tenant UUID, and their backend children must
prove that cross-tenant resources, counts, and delivery targets stay separate.
Logs and telemetry should carry the opaque tenant UUID and correlation ID,
while excluding invitation tokens, evidence content, and client names from
routine labels. This is a contract for later features, not a claim that every
future surface already exists.

The first deployment trusts one configured OIDC authority. The selected
expansion path is an identity broker with organization-specific connections
and one application-trusted issuer; enabling another authority requires an
issuer-validation and account-linking review. An external identity is keyed by
exact issuer plus subject and belongs to one `PlatformUser`. Two identities
with the same email do not merge automatically. An explicit HTTP-only link
operation now requires both a signed Bdgrz browser session and a validated OIDC
resource token in one request. It binds a new issuer-plus-subject to the session's
platform user ID; a provider identity already owned by another user conflicts.
Repeating the same link is safe, and ordinary OIDC continuation still creates a
separate user rather than inferring consent from an existing cookie. This is a
technical linking path, not approval to enable multiple authorities. M0-A07
still needs a security review of provider configuration, reauthentication
requirements, identity replacement and revocation, and recovery before that
deployment expands.

Client-provided slugs can reveal a name in browser history or server logs.
`Referrer-Policy: no-referrer` prevents browser referrer disclosure, and
operators should use a neutral slug when a client name is confidential. The
firm's client-naming and log-retention policy remains an M0-A07 product
decision; the service cannot infer confidentiality from the slug string.

## Alternatives and consequences

Self-service organization creation would turn any signup into a tenant administrator, so provisioning remains operator controlled. Granting the operator tenant access by default would create an unrequested cross-client access path. Using email or slug as a tenant identity would make renames and identity-provider changes unsafe. Storing roles in identity-provider tokens would delay revocation and blur the tenant boundary.

The API and MCP surfaces use Portia command/query authorization and event-sourced aggregates, reactors, and Fitz projections. Invitation acceptance and email verification are human HTTP flows and are intentionally absent from MCP. Projection reads can lag; security decisions for suspension use the tenant aggregate and permission checks. The mock delivery service sends no external message and must be replaced before real users can complete invitations.

## Public references

- No external normative source governs the tenant, operator, invitation, slug, and affiliation decisions; these are original product decisions recorded here and in [the domain model](../../product/domain-model.md).
- The existing issuer-plus-subject identity binding follows [OpenID Connect Core 1.0, December 2023](https://openid.net/specs/openid-connect-core-1_0.html) as a reference, under the [OIDF implementation-license information](https://openid.net/intellectual-property/openid-foundation-contribution-agreements/) recorded in [the source-reference policy](../../product/source-reference-policy.md). No specification prose or schema is copied here.

## Follow-up

M0-D25 and M0-A07 still own cross-client firm practice designation, engagement-specific staff grants, multi-authority federation, reviewed identity replacement and revocation, and full tenant-context conformance. M0-D28 remains the approval gate for the broader canonical catalog. These decisions should be incorporated into the later features that introduce those records.
