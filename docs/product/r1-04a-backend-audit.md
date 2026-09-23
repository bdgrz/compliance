# R1-04a backend acceptance audit

Status: in progress, 2026-09-23. Backend child #183 tracks this slice of #93;
frontend child #184 and the product parent remain open.

The first backend bundle adds an administrator-authorized invitation command with
an explicit built-in role, and an administrator-only read of the member's
effective team, role, and permission paths. Static HTTP path segments use
kebab-case; interpolated path values, JSON properties, query strings, and MCP
argument/property names use snake_case. Invitation acceptance and
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
`GET /api/v1/tenants/{tenant_id}/member-invitations` and read-only
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
PR #367 adds worker-owned SMTP delivery after the invitation event commits.
The token is derived from a shared key ring; only its hash, key ID, and attempt
ID persist. Delivery sent or failed is recorded in the invitation stream. A
worker restart retries an unacknowledged attempt with the same token and
`Message-ID`; a recorded key or SMTP failure requires administrator reissue.
Tests inject a mock sender for standalone and split-host proof.

## Remaining before closing #183

- Apply the accepted M0-D03 role, M0-D25 firm access, and M0-D28 entity rules to
  the remaining member lifecycle. Firm-staff membership alone grants no
  business-record access.
- Define and prove M0-A07 identity replacement, revocation, reauthentication,
  and lost-identity recovery while preserving historical authorship.
- Complete child-specific denied, concurrency, replay, lag, and recoverability
  evidence before closing #183. Keep #93 open for later frontend delivery.
