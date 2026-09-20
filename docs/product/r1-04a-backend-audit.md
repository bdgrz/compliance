# R1-04a backend acceptance audit

Status: in progress, 2026-09-20. Backend child #183 tracks this slice of #93;
frontend child #184 and the product parent remain open.

The first backend bundle adds an administrator-authorized invitation command with
an explicit built-in role, and an administrator-only read of the member's
effective team, role, and permission paths. The HTTP route, JSON properties,
query strings, and MCP contracts use snake_case. Invitation acceptance and
email proof remain personal HTTP operations. The selected role is stored in
the tenant invitation history and applied by the existing registration reactor
to the existing tenant RBAC graph after acceptance. Firm-staff invitations do
not gain standing access through this command.

`MemberAccessE2ETests.ShouldExplainSelectedRoleGivenAcceptedMemberInvitation`
proves the flow in standalone and independent API/worker modes with mocked
email delivery. `TenantInvitationTests` covers invalid roles, pending reissue,
token replacement, unchanged affiliation and purpose on reissue, and accepted
role history. `PermissionProjectionStateTests`
checks explanations against grant removal. A caller may use
`expected_built_in_role` to receive a conflict while the access projection is
behind the accepted invitation.

The invitation-status bundle adds administrator-authorized
`GET /api/v1/tenants/{tenant_id}/member_invitations` and read-only
`bdgrz.tenant-invitation.list`. The tenant-routed Fitz projector stores email,
affiliation, selected role, expiry, inviter, and accepted user; it never stores
or returns a token or token hash. A filtered query normalizes `email_address`.
The list is eventually consistent: a prior pending row can remain visible while
a reissued invitation has not yet projected. Callers may poll for a known
change but must not treat a status match as a causal revision fence.
`pending` and `expired` are calculated from the latest projected invitation
and current time. Acceptance reports `accepted_pending_activation` until the
member registration projects, then `active` even if a role is later removed.
Role readiness is exposed separately by `GetMemberAccess` with
`expected_built_in_role`. Firm-staff invitations require membership but have
no standing role grant.
The status endpoint is an administrative view; acceptance remains HTTP-only.

`TenantInvitationDirectoryTests` covers reissue, replay, expiration,
tenant-routed storage, membership activation lag, and omission of token hashes.
`MemberAccessE2ETests` verifies pending and active reads, denied reads,
eventual projection, and MCP parity in standalone and split API/worker modes.
Delivery failures are not represented as a durable status: delivery currently
occurs after the invitation event commits and the mock sender has no outbox.

## Remaining before closing #183

- Review M0-D03 role scope and M0-D25 firm access policy. This bundle reports
  the existing tenant roles; it does not approve a new policy or a standing
  firm-staff grant.
- Complete the provider identity replacement and recovery decision in M0-A07,
  preserving the platform user and member's historical authorship.
- Add durable invitation delivery and failure/retry status before a real email
  adapter replaces the mock. The current view distinguishes pending, expired,
  accepted-but-projecting, and active membership; it cannot claim delivery.
- Confirm the canonical entity and source-use decision in M0-D28, then complete
  child-specific denied, concurrency, replay, lag, and recoverability evidence
  before closure. Keep #93 open for later frontend delivery.
