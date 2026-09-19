# Client tenancy and operator provisioning

Status: implementation decision for the R1-15 API/MCP slice, 2026-09-19.
Decision owner: product owner and tech lead; acceptance remains tracked in M0-D25 and M0-A07.

## Decision

- A client organization is the only tenant boundary. `tenant_id` is its immutable, opaque UUID in APIs, events, jobs, and storage realms; `Organization` is the product name. Every client business record belongs to exactly one tenant. Platform-level criteria editions, methodology templates, and the firm-staff directory may exist outside tenants but cannot contain client records.
- Platform operators are platform users whose UUIDs are explicitly configured in `PlatformOperators:UserIds`. No production operator is inferred from authentication claims, email domains, or tenant membership. Developer identities are operators only in the local developer-authentication mode.
- Registration records display name, legal name, slug, and operator attribution. It creates a provisioning tenant and an email invitation for the first client administrator. The operator receives no implicit membership. Acceptance requires the invited user to own and verify the email address, then grants an explicit `client_personnel` membership and the administrator team. Only then does the tenant become active. Invitation delivery is mocked until a real email adapter is supplied.
- A firm-staff invitation creates a `firm_staff` membership when accepted. Membership alone grants no standing access; a later service-engagement assignment must grant appropriate access. Advisory or attest practice designation belongs in a future platform-level staff record, not in the tenant membership.
- Suspension preserves events, projections, and memberships while tenant-scoped authorization reads the event-sourced current lifecycle and denies immediately. Reactivation restores access without recreating records. System reactions may finish cleanup and bootstrap while suspended.
- Browser selection is derived from the signed-in user's membership list. There is no server-side mutable “active tenant” claim or token role. Every tenant API path carries `tenant_id`, which the server authorizes. The browser may retain one selected tenant in navigation state; changing selection must clear tenant-specific client state when UI work begins.
- Browser slugs are attributes. The server normalizes and validates them against a single reserved-route registry. New slugs are reserved in an event-sourced slug aggregate before a tenant switches. Old slugs retain their owner, become permanently unavailable for new registrations, and resolve only for an authenticated member of that active tenant. Unknown and inaccessible slugs return the same not-found response.
- One platform user is bound to an exact issuer-plus-subject identity and can hold several memberships. A future multi-organization identity broker with per-organization connections is preferred over trusting arbitrary issuers in each tenant request. The current single-authority login remains the initial deployment configuration; issuer federation and identity linking require a separate security review before more authorities are enabled.

## Alternatives and consequences

Self-service organization creation would turn any signup into a tenant administrator, so provisioning remains operator controlled. Granting the operator tenant access by default would create an unrequested cross-client access path. Using email or slug as a tenant identity would make renames and identity-provider changes unsafe. Storing roles in identity-provider tokens would delay revocation and blur the tenant boundary.

The API and MCP surfaces use Portia command/query authorization and event-sourced aggregates, reactors, and Fitz projections. Invitation acceptance and email verification are human HTTP flows and are intentionally absent from MCP. Projection reads can lag; security decisions for suspension use the tenant aggregate and permission checks. The mock delivery service sends no external message and must be replaced before real users can complete invitations.

## Public references

- No external normative source governs the tenant, operator, invitation, slug, and affiliation decisions; these are original product decisions recorded here and in [the domain model](../../product/domain-model.md).
- The existing issuer-plus-subject identity binding follows [OpenID Connect Core 1.0, December 2023](https://openid.net/specs/openid-connect-core-1_0.html) as a reference, under the [OIDF implementation-license information](https://openid.net/intellectual-property/openid-foundation-contribution-agreements/) recorded in [the source-reference policy](../../product/source-reference-policy.md). No specification prose or schema is copied here.

## Follow-up

M0-D25 and M0-A07 still own cross-client firm practice designation, engagement-specific staff grants, multi-authority federation, identity linking, and full split-host conformance. M0-D28 remains the approval gate for the broader canonical catalog. These decisions should be incorporated into the later features that introduce those records.
