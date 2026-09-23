# Client tenancy and operator provisioning

Status: implementation decision for the R1-15 API/MCP slice, 2026-09-19;
amended for accepted M0-D25 and M0-A07 decisions, 2026-09-23.
Decision owner: product owner and tech lead; backend acceptance remains in #152.

> [!NOTE]
> Superseded in part by [ADR 0009](0009-tenant-identity-federation-and-context.md),
> accepted for M0-A07 on 2026-09-22. It replaces the identity-broker preference
> with directly validated multiple trusted issuers, accepts the tenant-context
> contract below. The verified self-service creation path is implemented;
> in-product operator grants and revocations remain under #152. Historical
> operator and invitation events retain their original replay semantics.

## Decision

- A client organization is the only tenant boundary. `tenant_id` is its immutable, opaque UUID in APIs, events, jobs, and storage realms; `Organization` is the product name. Every client business record belongs to exactly one tenant. Platform-level criteria editions, methodology templates, and the firm-staff directory may exist outside tenants but cannot contain client records.
- Platform operators are platform users whose UUIDs are explicitly configured in `PlatformOperators:UserIds`. No production operator is inferred from authentication claims, email domains, or tenant membership. Developer identities are operators only in the local developer-authentication mode.
- A platform operator is the platform super administrator for platform operations: suspension, reactivation, slug administration, firm-staff invitations, and inspection of organization metadata and memberships. The operator portfolio is a paginated platform-scoped query at `GET /api/v1/platform/tenants` and a read-only MCP tool. It includes provisioning and suspended organizations. Operator status alone confers no client business-record access. Operator grant and revocation still require the in-product roster tracked by #152.
- Production registration records display name, legal name, slug, and the signed-in creator's platform user ID. The server finds a verified email in that user's directory; the request does not supply an administrator email. `TenantRegistered` records that creator as the first Org Admin with `client_personnel` affiliation; the independent worker materializes the membership and administrator-team grant from that event. Access fails closed while projections catch up. Historical operator-provisioned registrations still replay the invited first-administrator flow, and local developer registration without an invitation still bootstraps its creator. Production email verification delivery and durable retry are tracked by #360.
- A firm-staff invitation creates a `firm_staff` membership when accepted. Neither membership nor a current or historical tenant team grant confers standing business access. Accepted service-engagement assignment is the later access path under #269. Advisory or attest practice designation belongs in a future platform-level staff record, not in the tenant membership.
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

Verified self-service creation lets a platform user create an organization and become its first Org Admin without operator action. Granting an operator tenant access by default would create an unrequested cross-client access path. Using email or slug as a tenant identity would make renames and identity-provider changes unsafe. Storing roles in identity-provider tokens would delay revocation and blur the tenant boundary.

The API and MCP surfaces use Portia command/query authorization and event-sourced aggregates, reactors, and Fitz projections. Invitation acceptance and email verification are human HTTP flows and are intentionally absent from MCP. Projection reads can lag; security decisions for suspension use the tenant aggregate and permission checks. Email verification challenge delivery uses a recoverable worker reactor and a configured STARTTLS SMTP adapter outside development. Invitation delivery still uses an in-process mock and remains a separate follow-up before real users can complete invitations.

## Public references

- No external normative source governs the tenant, operator, invitation, slug, and affiliation decisions; these are original product decisions recorded here and in [the domain model](../../product/domain-model.md).
- The existing issuer-plus-subject identity binding follows [OpenID Connect Core 1.0, December 2023](https://openid.net/specs/openid-connect-core-1_0.html) as a reference, under the [OIDF implementation-license information](https://openid.net/intellectual-property/openid-foundation-contribution-agreements/) recorded in [the source-reference policy](../../product/source-reference-policy.md). No specification prose or schema is copied here.

## Follow-up

M0-D25 and M0-A07 are accepted in the product decision and ADR 0009. #152
still owns the in-product operator roster and remaining tenant acceptance;
#269 owns engagement-specific staff grants; #360 owns production verification
delivery and retry. Later features must prove tenant isolation for their own
records and surfaces.
