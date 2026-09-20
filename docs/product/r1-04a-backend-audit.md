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

## Remaining before closing #183

- Review M0-D03 role scope and M0-D25 firm access policy. This bundle reports
  the existing tenant roles; it does not approve a new policy or a standing
  firm-staff grant.
- Complete the provider identity replacement and recovery decision in M0-A07,
  preserving the platform user and member's historical authorship.
- Expose and verify pending versus active member lifecycle state, including
  failed and expired invitations, without disclosing tokens or other tenants.
- Confirm the canonical entity and source-use decision in M0-D28, then complete
  child-specific denied, concurrency, replay, lag, and recoverability evidence
  before closure. Keep #93 open for later frontend delivery.
