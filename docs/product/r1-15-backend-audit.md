# R1-15 backend acceptance audit

Status: in progress, 2026-09-23. This tracks the backend child #152 of product
story #127. The product story remains open for its browser criteria.

## Implementation and behavioral evidence

| Requirement | Evidence | Current limit |
| --- | --- | --- |
| Verified self-service creation and creator Org Admin bootstrap | `RegisterTenantAuthorizerTests`, `RegisterTenantRequestScenarioTests.ShouldCreateTenantGivenVerifiedCreatorWithoutOperatorGrant`, `TenantRbacBootstrapReactorTests.ShouldBootstrapCreatorGivenVerifiedSelfServiceRegistration`, `SplitHostTenantE2ETests.ShouldBootstrapVerifiedCreatorWithoutOperatorGivenSeparateApiAndWorker` | One registration event records creator/admin intent, but slug confirmation can activate the tenant before the worker materializes membership and grants. An activation fence with split-host recovery proof is still required for #152's atomic first-admin acceptance. Production challenge delivery is pending #360. |
| Historical operator and invitation replay | `TenantContractCompatibilityTests.ShouldRemainActiveGivenLegacyRegistrationWithoutInvitation`, `TenantRbacBootstrapReactorTests.ShouldPreserveInvitationBootstrapGivenLegacyRegistration`, `TenantInvitationE2ETests.ShouldGrantTenantAccessGivenVerifiedAcceptedAdministratorInvitation` | New production registration is self-service; old events and local developer flows retain their original meaning. |
| First administrator verifies email, accepts invitation, receives membership, and activates tenant | `TenantInvitationE2ETests.ShouldGrantTenantAccessGivenVerifiedAcceptedAdministratorInvitation`, `SplitHostTenantE2ETests.ShouldCompleteAdministratorAndSlugFlowGivenIndependentWorker` | Delivery is mocked; no external email is sent. |
| Firm staff affiliation without standing business access, even after a historical team grant | `TenantAccessAuthorizerTests.ShouldDenyFirmStaffGivenStandingTeamPermission`, `FitzPermissionAuthorizerTests.ShouldDenyFirmStaffGivenPersistedPreUpgradePermissionGrant`, `TenantInvitationE2ETests.ShouldGrantTenantAccessGivenVerifiedAcceptedAdministratorInvitation`, and `SplitHostTenantE2ETests.ShouldBootstrapVerifiedCreatorWithoutOperatorGivenSeparateApiAndWorker` exercise direct permission checks and two-tenant HTTP/MCP denial after a projected administrator-team grant | Accepted engagement assignment and practice designation remain F1-07 backend #269. |
| Slug history, permanent reservation, concurrent claims, and route collision | `TenantSlugTests`, `ReservedTenantRouteCollisionCheckTests`, `SplitHostTenantE2ETests.ShouldProjectTenantLifecycleGivenIndependentApiAndWorker`, `SplitHostTenantE2ETests.ShouldCompleteAdministratorAndSlugFlowGivenIndependentWorker` | Route registry must continue to cover new top-level routes. |
| Client slug referrer privacy | `ComplianceWebTests.ShouldServeSpaFallbackGivenNonApiRoute`, `ComplianceWebTests.ShouldExposePublicAuthSettingsGivenExternalConfiguration` | Client names can still appear in the browser's own history and server logs; product naming and log retention require M0-A07. |
| Immediate suspension and reactivation | `TenantTests.ShouldPreserveHistoryGivenSuspensionAndReactivation`, `SplitHostTenantE2ETests.ShouldCompleteAdministratorAndSlugFlowGivenIndependentWorker`, `SplitHostTenantE2ETests.ShouldProjectTenantLifecycleGivenIndependentApiAndWorker` | Read projections may lag; the authorization path reads current tenant activity. |
| Denied and missing tenant disclosure, counts, and notifications | `SplitHostTenantE2ETests.ShouldProjectTenantLifecycleGivenIndependentApiAndWorker`, `SplitHostTenantE2ETests.ShouldCompleteAdministratorAndSlugFlowGivenIndependentWorker`, `ProgramE2ETests` | Coverage applies to current tenant, membership, team, and program surfaces. Later search, artifacts, exports, and jobs need their own tenant leak tests. |
| URL tenant authority and separate external identities | `ProgramE2ETests.ShouldKeepProgramChangesScopedGivenTwoTenants` supplies a conflicting body tenant ID; `UserIdentityContinuationTests.ShouldKeepIdentitySeparateGivenExistingSessionAndNewIssuer` proves an unrelated issuer remains a separate user. `UserIdentityContinuationWebTests.ShouldLinkOnlyWithSessionAndProviderProofGivenExternalAuthentication` and `IdentityLinkE2ETests.ShouldPreserveLinkedUserGivenIndependentApiAndWorker` prove explicit dual-proof linking and cross-host continuation. | M0-A07 still requires review of multi-authority configuration, identity replacement and revocation, and recovery. Broader export, artifact, and job leak tests remain open under EN-01. |
| Recovery, concurrency, and replay | `SplitHostTenantE2ETests.ShouldRecoverInvitationGivenFirstAdministratorDeliveryFailure`, `SplitHostTenantE2ETests.ShouldRetryDirectInvitationGivenDeliveryFailure`, `SplitHostTenantE2ETests.ShouldProjectTenantLifecycleGivenIndependentApiAndWorker`, and `TenantOwnerTests` | Direct invitation delivery failure requires the caller to retry. Durable delivery is needed before replacing the mock adapter. |
| Standalone and independent API/worker modes | `TenantInvitationE2ETests`, `SplitHostTenantE2ETests`, `ProgramE2ETests` | Each new tenant-scoped feature must repeat relevant parity proof. |

PRs #151 and #153 through #159 delivered the earlier implementation and proof;
their issue comments record exact-head CI and merge evidence. This audit does
not reinterpret those merges as completion of #152. The current proof PR must
still pass full Release nonbroker and broker suites, formatting, Native AOT
containers on both CI architectures, review, exact-head merge, and main parity.

## Decisions and remaining acceptance

- M0-D25 is accepted: firm staff have no standing business access; #269 will
  introduce accepted engagement assignments. Offboarding retention is #346.
- M0-A07 is accepted in ADR 0009. Each new surface still needs its own
  tenant-isolation and split-host proof.
- M0-D28's catalog decision is accepted. EN-01's broader leak matrix must extend as search, artifacts,
  exports, and background work are introduced.
- AUTH-02a backend #360 must replace mock verification delivery and add durable
  challenge retry before production users can complete verified registration.
- Slug confirmation can mark a tenant active before creator membership and Org
  Admin grants materialize. #152 still needs an activation fence and split-host
  recovery proof so registration cannot expose an active tenant without its
  first administrator.
- The follow-on #152 slice should keep the platform operator roster in its own
  event stream for the atomic last-operator invariant, seed it only before it
  has ever been initialized, and check current event state when granting or
  revoking. That slice should activate tenants only after first-administrator
  RBAC materialization, with replay and split-host recovery proof.
- In-product operator grant and revocation and the rest of #152 acceptance
  remain open after the self-service and firm-staff access slice.
- Close backend child #152 only when its own acceptance evidence and inherited
  dependencies are complete. Keep product parent #127 open for UI delivery.
