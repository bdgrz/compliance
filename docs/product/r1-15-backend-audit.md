# R1-15 backend acceptance audit

Status: in progress, 2026-09-23. This tracks the backend child #152 of product
story #127. The product story remains open for its browser criteria.

## Implementation and behavioral evidence

| Requirement | Evidence | Current limit |
| --- | --- | --- |
| Verified self-service creation and creator Org Admin bootstrap | `RegisterTenantAuthorizerTests`, `RegisterTenantRequestScenarioTests.ShouldCreateTenantGivenVerifiedCreatorWithoutOperatorGrant`, `ActivateTenantReadinessTests`, `SplitHostTenantE2ETests.ShouldBootstrapVerifiedCreatorWithoutOperatorGivenSeparateApiAndWorker`, `SplitHostTenantE2ETests.ShouldKeepVerifiedTenantProvisioningUntilIndependentWorkerCompletesGrantsGivenDelayedWorker` | `TenantActivated` is the visibility fence after slug confirmation, client-personnel membership, administrator-team assignment, and effective grants. Worker retry and restart complete a registration that was recorded while the worker was stopped. Production challenge delivery shipped in PR #363; a live SMTP relay was not exercised. |
| In-product operator authority and last-operator protection | `PlatformOperatorRosterTests`, `FitzPlatformUserDirectoryTests`, `OperatorPortfolioE2ETests.ShouldListPlatformTenantMetadataGivenConfiguredOperatorAndHostMode` | HTTP and MCP grant, list, and revoke use the current roster stream in standalone and split hosts. The identity projector rejects grants to unknown UUIDs so a nonexistent grantee cannot become the only operator. Events carry actor ID and Bdgrz-session display snapshot, subject, time, and reason; bootstrap runs only before first initialization. A restarted API does not restore a revoked operator. |
| Historical operator, self-service, and invitation replay | `TenantContractCompatibilityTests.ShouldRemainActiveGivenLegacyRegistrationWithoutInvitation`, `TenantContractCompatibilityTests.ShouldRemainActiveGivenSelfServiceRegistrationBeforeActivationFence`, `TenantRbacBootstrapReactorTests.ShouldPreserveInvitationBootstrapGivenLegacyRegistration`, `TenantInvitationE2ETests.ShouldGrantTenantAccessGivenVerifiedAcceptedAdministratorInvitation` | The optional activation flag defaults off for registrations written before the fence. New production registrations set it explicitly; old events and local developer flows retain their original meaning. |
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
- AUTH-02a backend #360 closed after PR #363 delivered retryable production
  verification mail; deployment with a live SMTP relay remains unproven.
- #152 still depends on #157's shared actor-attribution and first-consumer
  authorization acceptance before closing. Registration and administrator
  grants are separate durable writes; tenant activation is the visibility fence.
- Close backend child #152 only when its own acceptance evidence and inherited
  dependencies are complete. Keep product parent #127 open for UI delivery.
