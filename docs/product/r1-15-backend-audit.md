# R1-15 backend acceptance audit

Status: in progress, 2026-09-19. This tracks the backend child #152 of product
story #127. The product story remains open for its browser criteria.

## Implementation and behavioral evidence

| Requirement | Evidence | Current limit |
| --- | --- | --- |
| Operator-only tenant creation and no implicit operator membership on the first-administrator path | `RegisterTenantAuthorizerTests`, `RegisterTenantRequestScenarioTests.ProductionOperatorMustProvideLegalNameAndFirstAdministrator`, `TenantInvitationE2ETests.FirstAdministratorMustVerifyAndAcceptBeforeTenantAccess`, `SplitHostTenantE2ETests.IndependentWorkerCompletesFirstAdministratorAndSlugFlow` | Production operator IDs must be explicitly configured. Legacy developer-mode registration without a first administrator bootstraps its creator. |
| First administrator verifies email, accepts invitation, receives membership, and activates tenant | `TenantInvitationE2ETests.FirstAdministratorMustVerifyAndAcceptBeforeTenantAccess`, `SplitHostTenantE2ETests.IndependentWorkerCompletesFirstAdministratorAndSlugFlow` | Delivery is mocked; no external email is sent. |
| Firm staff affiliation without administrator grant | `TenantInvitationTests.FirmStaffInvitationCannotGrantAdministrator`, both invitation end-to-end flows | Practice designation and engagement grants await M0-D25 and later firm operations. |
| Slug history, permanent reservation, concurrent claims, and route collision | `TenantSlugTests`, `ReservedTenantRouteCollisionCheckTests`, `SplitHostTenantE2ETests.ApiAndIndependentWorkerProjectTenantLifecycle`, `SplitHostTenantE2ETests.IndependentWorkerCompletesFirstAdministratorAndSlugFlow` | Route registry must continue to cover new top-level routes. |
| Client slug referrer privacy | `ComplianceWebTests.ShouldServeSpaFallbackOutsideApiBoundary`, `ComplianceWebTests.ShouldExposeOnlyPublicExternalAuthenticationConfiguration` | Client names can still appear in the browser's own history and server logs; product naming and log retention require M0-A07. |
| Immediate suspension and reactivation | `TenantTests.ShouldBlockAndRestoreAccessWithoutDiscardingTenantHistory`, `SplitHostTenantE2ETests.IndependentWorkerCompletesFirstAdministratorAndSlugFlow`, `SplitHostTenantE2ETests.ApiAndIndependentWorkerProjectTenantLifecycle` | Read projections may lag; the authorization path reads current tenant activity. |
| Denied and missing tenant disclosure, counts, and notifications | `SplitHostTenantE2ETests.ApiAndIndependentWorkerProjectTenantLifecycle`, `SplitHostTenantE2ETests.IndependentWorkerCompletesFirstAdministratorAndSlugFlow`, `ProgramE2ETests` | Coverage applies to current tenant, membership, team, and program surfaces. Later search, artifacts, exports, and jobs need their own tenant leak tests. |
| URL tenant authority and separate external identities | `ProgramE2ETests.CreateReviseAndReadProgramRemainScopedToItsTenant` supplies a conflicting body tenant ID; `UserIdentityContinuationTests.ShouldNotLinkNewOidcIdentityFromAnExistingSession` proves an unrelated issuer remains a separate user | A reviewed identity-linking flow and broader export, artifact, and job leak tests remain open under M0-A07 and EN-01. |
| Recovery, concurrency, and replay | `SplitHostTenantE2ETests.WorkerRecoversFromFirstAdministratorDeliveryFailure`, `SplitHostTenantE2ETests.DirectInvitationCanBeRetriedAfterDeliveryFailure`, `SplitHostTenantE2ETests.ApiAndIndependentWorkerProjectTenantLifecycle`, and `TenantOwnerTests` | Direct invitation delivery failure requires the caller to retry. Durable delivery is needed before replacing the mock adapter. |
| Standalone and independent API/worker modes | `TenantInvitationE2ETests`, `SplitHostTenantE2ETests`, `ProgramE2ETests` | Each new tenant-scoped feature must repeat relevant parity proof. |

PRs #151 and #153 through #159 delivered the earlier implementation and proof;
their issue comments record exact-head CI and merge evidence. This audit does
not reinterpret those merges as completion of #152. The current proof PR must
still pass full Release nonbroker and broker suites, formatting, Native AOT
containers on both CI architectures, review, exact-head merge, and main parity.

## Decisions and remaining acceptance

- M0-D25 must settle firm-authored material retention and visibility and whether
  firm staff receive only engagement assignments or any standing access. The
  technical tenant default is in ADR 0001; these operating policy facts are
  not inferred from code.
- M0-A07 still owns the accepted routing, federation, identity-linking, and
  tenant-context ADR and its complete split-host spike. The current single
  authority deployment and route-safety proof cover only a subset.
- M0-D28 still owns approval of the full canonical entity and source-rights
  catalog. EN-01's broader leak matrix must extend as search, artifacts,
  exports, and background work are introduced.
- Close backend child #152 only when its own acceptance evidence and inherited
  dependencies are complete. Keep product parent #127 open for UI delivery.
