# Tenant identity, federation, and tenant context

Status: accepted for M0-A07, 2026-09-22. Decision owner: Jeff Repanich
(product owner and tech lead). Date: 2026-09-22.

This record accepts the tenant-identity and tenant-context contract that
[ADR 0001](0001-client-tenancy-and-operator-provisioning.md) described as a
technical default. It supersedes the ADR 0001 statements that preferred an
identity broker and that kept organization creation and operator grants out of
the product. The remaining ADR 0001 provisioning mechanics stay current until
R1-15 adopts the consequences below.

## Decision

### Organization identity

- A client organization is the only tenant. Its `tenant_id` is an immutable,
  opaque, non-sequential UUID. It is the only organization identifier in API
  contracts, MCP inputs, events, Fitz realms, jobs, cache keys, logs, and
  telemetry.
- Every organization-scoped HTTP route begins
  `/api/v1/tenants/{tenant_id}/`. The server verifies the caller's active
  membership and grant for that `tenant_id` on every request. It never trusts
  an organization identifier in a request body; the route value wins. MCP tools
  carry one explicit `tenant_id` and run the same request authorizer.
- Only sign-in, self-scoped (`/api/v1/my/`, `/api/v1/users/{user_id}/`,
  `/api/v1/tenants/mine`, `/api/v1/tenant-slugs/{slug}/mine`), organization
  creation (`POST /api/v1/tenants`), and platform-operator routes may omit
  `tenant_id`.
- An organization has one collaboration workspace. Programs and engagements
  provide sub-scoping inside it.

### Browser slugs

- Browser routes identify the organization by an administrator-chosen,
  URL-friendly slug (for example `/acme-corp/controls`). A slug may be the
  client's name.
- The browser resolves a slug to `tenant_id` only from the signed-in user's
  own memberships. An unknown slug and an inaccessible slug produce the same
  not-found result.
- A slug change keeps the retired slug with its owner for member redirects.
  A retired slug is never reassigned to another organization.
- One reserved-route registry (`TenantSlugs`) is shared by server validation
  and a repository check. The check fails when a top-level server or client
  route is missing from the registry. API startup fails when a newly reserved
  route collides with a current or retired organization slug.
- Because a slug can name a client, the API host sends
  `Referrer-Policy: no-referrer` on browser routes, static assets, and API
  responses. Request paths never reach logs or log scopes. The host disables
  `Microsoft.AspNetCore.Hosting.Diagnostics`, which emits request start and
  finish events and the `RequestPath` scope. It also holds every other
  `Microsoft.AspNetCore` category at `Warning` or above. This runs after
  configuration binding, so neither a category override nor a
  provider-specific override can re-enable path logging. Compliance code logs
  `tenant_id` and correlation IDs, never slugs or client names.

### Identity and federation

- The application trusts several OIDC issuers directly, without a broker.
  Each trusted issuer is a configured resource with its own authority,
  audience, metadata, and signing keys, and is validated as its own JWT bearer
  scheme. A token is accepted only if the issuer's own keys verify it and its
  `iss` matches that issuer. Tokens from unknown issuers are rejected, and so
  are tokens that name one trusted issuer but are signed with another issuer's
  key.
- In R1 the trusted-issuer set is deployment configuration. Per-organization
  issuer registration by a client administrator is delivered by
  [F1-06 #268](https://github.com/bdgrz/compliance/issues/268). A registered
  issuer establishes authentication only. It never grants membership.
- An external identity is the exact `iss` plus `sub`. It binds once to one
  `PlatformUser`, which may hold memberships in several organizations. The
  same subject under two issuers is two identities and, unless linked, two
  users.
- One person with two sign-in identities links them with the explicit
  HTTP-only proof-of-both operation delivered in PR #180. That operation
  requires a signed Bdgrz session and a validated provider token in one
  request. Nothing merges users automatically by email address, and ordinary
  continuation never infers a link from an existing cookie.
- The platform is the only authority for membership, roles, and grants.
  Token claims never carry organization membership or roles, and any such
  claim is ignored.

### Tenant context propagation

- Fitz tenant-owned streams use the `tenant_id` as their realm. Projector
  reads and checkpoints use that realm.
- A reactor, job, or message keeps the originating `tenant_id`, its
  triggering event, and a named system actor. It re-verifies the tenant's
  lifecycle and the request authorizer before acting, rather than inheriting
  trust from its producer.
- New cache keys, search indexes, artifact paths, exports, jobs, and
  notifications include the `tenant_id`.

### Cross-tenant leak test strategy

Every tenant-owned surface is covered by these layers:

1. **Composition.** Portia fails closed when a request lacks an authorizer
   (PR #325), and every business endpoint declares the `BdgrzApiUser` policy.
2. **Route contract.** An architecture test requires every `/api/v1/` route to
   select its organization with a leading `/api/v1/tenants/{tenant_id}/`, or
   to be on the explicit self, platform, and sign-in allowlist. It also rejects
   alternative organization identifiers in tenant routes.
3. **Two-tenant broker proofs per surface.** A member of tenant B, and an
   outsider, must get the same not-found result for tenant A's resource as for
   a nonexistent one. Lists, counts, and pagination cursors must exclude tenant
   A, and a cursor issued in A must not be valid in B. Existing coverage
   includes programs, client services, boundaries, applications, control,
   commitment, and risk drafts, snapshots, members and invitations, the
   operator portfolio, and recovery replay. Each later search, artifact,
   export, projection, job, reactor, and notification backend child must add
   the same proof for its surface.
4. **Split host.** At least one two-tenant proof per surface runs with
   independent API and worker hosts (`SplitHostTenantE2ETests`,
   `IdentityLinkE2ETests`, `ProgramRecoveryE2ETests`).
5. **Telemetry.** A host test drives slug browser and slug-resolution requests
   with verbose framework logging and fails if the slug appears in any log
   message, state value, or scope.

## Evidence

| Decision point | Evidence |
| --- | --- |
| Route tenant authority and body-value rejection | PR #165: two-tenant program broker test with a conflicting body `tenant_id` |
| Slug registry and startup collision check | PR #156; `ReservedTenantRouteTests`, `ReservedTenantRouteCollisionCheckTests` |
| Uniform not-found for unknown and inaccessible slugs | `TenantInvitationE2ETests` (unknown and non-member slug resolution both return 404) |
| Slug resolution with independent API and worker hosts | `SplitHostTenantE2ETests.ShouldCompleteAdministratorAndSlugFlowGivenIndependentWorker` |
| Referrer policy | PR #164; `ComplianceWebTests.ShouldServeSpaFallbackGivenNonApiRoute` |
| Slugs absent from logs and scopes under category and provider overrides | `TenantSlugLoggingTests` (this change) |
| Direct validation of several issuers, issuer/key confusion rejected, same subject under two issuers yields two users | `MultipleTrustedIssuerWebTests` (this change) |
| Explicit dual-proof identity linking, continuation across independent API and worker | PR #180; `UserIdentityContinuationWebTests`, `IdentityLinkE2ETests` |
| Route contract for tenant selection | `ComplianceWebTests.ShouldScopeOrganizationRoutesByTenantIdGivenBusinessApiEndpoints` (this change) |
| Fail-closed request authorization composition | PR #325 |
| Tenant lifecycle projection across independent API and worker | `SplitHostTenantE2ETests.ShouldProjectTenantLifecycleGivenIndependentApiAndWorker` |

Both host entry points register the log redaction. Request paths exist only in
the API host, which serves HTTP in both standalone and split mode.

## Options considered

- **Identity broker with per-organization connections** (for example Auth0
  Organizations or Entra External ID). With a broker, the application trusts
  one issuer and the broker owns federation. Rejected: it adds a vendor
  dependency and cost for every client connection. Validating each issuer as
  its own scheme is already isolated and tested.
- **Opaque system-generated slugs.** These give maximum URL confidentiality
  but unfriendly links. Rejected in favor of readable slugs protected by the
  referrer policy and log redaction.
- **Organization or role claims in tokens.** Rejected because revocation
  would be delayed, and a client's identity provider would become an
  authority over platform access.
- **Email-based automatic account merging.** Rejected: email ownership at one
  issuer is not proof of identity at another.

## Consequences

- Adding a trusted issuer is a configuration change that adds a validated
  scheme. It needs no code change and must not weaken validation of existing
  issuers. Resource schemes are numbered in configuration-key order.
- Operators who need request diagnostics use trace and correlation IDs.
  Request paths are deliberately unavailable in logs.
- Jeff Repanich decided the following on 2026-09-22.
  [R1-15 #127](https://github.com/bdgrz/compliance/issues/127) and its backend
  child [#152](https://github.com/bdgrz/compliance/issues/152) must adopt them;
  this ADR does not implement them:
  - Organization creation is self-service. Any signed-in user may create an
    organization and becomes its first administrator. This replaces the
    operator-only provisioning in ADR 0001.
  - An existing operator grants and revokes platform-operator status in the
    product, with an audit log. This replaces operator lists held only in
    deployment configuration.
  - Operators see organization metadata only: lifecycle, the administrator
    roster, and usage. They see no business records unless they also hold a
    membership or engagement assignment.
  - Firm staff reach client records only through an accepted engagement
    assignment.
- These items stay with their owning issues and do not block this decision:
  - Identity replacement, revocation, reauthentication requirements, and
    lost-identity recovery belong to
    [R1-04a #183](https://github.com/bdgrz/compliance/issues/183).
  - Whether an organization may require its members to sign in through its
    own issuer belongs to F1-06 #268.

## Public references

- [OpenID Connect Core 1.0, December 2023](https://openid.net/specs/openid-connect-core-1_0.html)
  §2 (the `iss` and `sub` pair identifies an end user) is used as a reference
  under the [OIDF implementation-license information](https://openid.net/intellectual-property/openid-foundation-contribution-agreements/),
  recorded in [the source-reference policy](../../product/source-reference-policy.md).
  No specification prose or schema is copied.
- The slug, workspace, and log-redaction rules are original product
  decisions.
