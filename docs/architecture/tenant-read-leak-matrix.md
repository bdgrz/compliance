# Tenant-Owned Read Leak Matrix

Issue #157 and ADRs 0002/0009 require two real tenants, an actor who may read both,
an outsider, and standalone plus split API/worker proof for every implemented
tenant-owned HTTP and MCP read. Check returned rows, counters, cursors, and
cross-tenant record IDs. A route that does not exist is not counted as covered.
The route inventory below follows `src/Compliance.App/Program.cs`.

| Read family | Current evidence | Remaining proof |
| --- | --- | --- |
| RBAC `/teams`, `/roles`, team members, role permissions, role teams and the matching MCP lists | `TenantReadLeakMatrixE2ETests.ShouldScopeRbacListsSearchAndCursorsGivenTwoTenants`: both host modes; two-tenant team/role search and HTTP/MCP cursors through exhaustion, role-permission search cursors through exhaustion, nested search, foreign-parent empty pages, and outsider denial. | Add multi-row cursor cases for team members and role teams when those relationships gain a suitable seed fixture. |
| Application import batch GET, `/rows`, `/preview` and matching MCP reads | `TenantReadLeakMatrixE2ETests.ShouldScopeImportCountsRowsAndPreviewGivenTwoTenants`: both modes; distinct nonzero row and invalid counters, row and preview content, HTTP row cursor and foreign-cursor rejection, cross-tenant IDs, outsider denial. | Add preview and MCP row cursor variants. The progress fields are currently initialized to zero and have no update path; prove their isolation with processing in #213. No import list or separate count route exists. |
| Application list, revisions, system instances, references and change preview | `ProjectionReadConsistencyE2ETests.ShouldNeverReturnStaleProjectionOrCrossTenantRowsGivenStandaloneOrSplitHost` covers application-list isolation in both modes. `ApplicationReadLeakMatrixE2ETests.ShouldKeepApplicationReadPagesAndPreviewsWithinTenantGivenTwoPopulatedTenants` adds HTTP/MCP exact revision and instance reads, `limit=1` revision, instance and application/instance reference lists through cursor exhaustion, strict foreign-cursor rejection, populated change previews, foreign IDs and outsider denial in both modes. [Detailed evidence](application-read-leak-evidence.md). | The top-level application list still lacks this two-populated-tenant cursor proof. The new fixture covers current draft boundary references, not approved versions, control references or stopped-worker replay. |
| Boundary program lists, versions, decisions and impact preview | `BoundarySnapshotReadLeakMatrixE2ETests.ShouldScopeBoundaryAndSnapshotReadsGivenTwoTenants` covers HTTP/MCP paged boundary, approved-version and decision lists through exhaustion, exact boundary/draft/version/effective/decision reads, and populated impact changes in both modes. Foreign IDs, outsider denial and strict foreign-cursor rejection are asserted. | One approved version per tenant is seeded; nested impact contributions can be empty. No separate count route exists. |
| Snapshot list, detail, verification and manifest regeneration | The same `BoundarySnapshotReadLeakMatrixE2ETests` case covers HTTP/MCP paged snapshot lists through exhaustion, exact snapshot, verification and regenerated manifest content in both modes, with foreign IDs, outsider denial and strict foreign-cursor rejection. `SnapshotE2ETests.ShouldFreezeExactScopeGivenStandaloneOrSplitWorker` supplies additional freeze/scope evidence. | Two snapshots per tenant are seeded; stopped-worker replay has separate capability evidence. No separate count route exists. |
| Program and client-service current, revision, list, history and setup-work reads | `ProgramReadLeakMatrixE2ETests.ShouldScopeProgramAndServicePagesGivenTwoTenants` covers HTTP/MCP exact current and revision reads, six paged list/history/setup-work families, seeded content and versions, strict foreign-cursor rejection, foreign IDs and outsider denial in both modes. [Detailed evidence](program-service-read-leak-evidence.md). | The fixture does not cover stopped-worker replay, Program stage transitions or future setup-work inputs beyond boundaries. These routes expose no separate count or search operation. |
| Platform metadata exception | `OperatorPortfolioE2ETests.ShouldListPlatformTenantMetadataGivenConfiguredOperatorAndHostMode` covers operator-only tenant metadata pagination in both modes. | Keep this separate from client business-record access. |
| Tenant detail, self-list, operator member list, invitation list, effective member access and matching MCP reads | `TenantIdentityReadLeakMatrixE2ETests.ShouldScopeTenantMemberInvitationAndSelfReadsGivenTwoTenants`: both modes; separate tenant owners and an outsider, operator metadata and member-list authority, HTTP/MCP self-list and invitation cursors through exhaustion, strict foreign invitation cursor rejection, foreign paths and email filters, and foreign member-access denial. [Detailed evidence](tenant-identity-read-leak-matrix.md). | Member-list pagination after a second membership activates and actor replacement/deprovisioning lifecycle proof. These reads have no total-count field. |

Still open for the same two-tenant, two-host HTTP/MCP matrix: control,
commitment and risk draft/current/history reads, the top-level application
list cursor and the remaining application contexts above. Existing capability
tests often prove standalone cross-tenant denial or split-host recovery, but that
does not prove every list and cursor in both modes.

The current API has no separate tenant-owned count, export, job, notification
or artifact-content route. Import batch status carries row and invalid counts
plus zero-valued progress fields. Add any future surface to this matrix in the
backend child that introduces it, including restricted-row filtering before
counting and pagination.
