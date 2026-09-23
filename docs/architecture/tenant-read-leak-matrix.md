# Tenant-Owned Read Leak Matrix

Issue #157 and ADRs 0002/0009 require two real tenants, an actor who may read both,
an outsider, and standalone plus split API/worker proof for every implemented
tenant-owned HTTP and MCP read. Check returned rows, counters, cursors, and
cross-tenant record IDs. A route that does not exist is not counted as covered.
The route inventory below follows `src/Compliance.App/Program.cs`.

| Read family | Current evidence | Remaining proof |
| --- | --- | --- |
| RBAC `/teams`, `/roles`, team members, role permissions, role teams and the matching MCP lists | `TenantReadLeakMatrixE2ETests.ShouldScopeRbacListsSearchAndCursorsGivenTwoTenants`: both host modes; two-tenant team/role search and HTTP/MCP cursors; nested search, foreign-parent empty pages, and outsider denial. | Add multi-row cursor cases for team members and role teams, and MCP role-permission cursors when those relationships gain a suitable seed fixture. |
| Application import batch GET, `/rows`, `/preview` and matching MCP reads | `TenantReadLeakMatrixE2ETests.ShouldScopeImportCountsRowsAndPreviewGivenTwoTenants`: both modes; distinct nonzero row and invalid counters, row and preview content, HTTP row cursor and foreign-cursor rejection, cross-tenant IDs, outsider denial. | Add preview and MCP row cursor variants. The progress fields are currently initialized to zero and have no update path; prove their isolation with processing in #213. No import list or separate count route exists. |
| Application list and boundary references | `ProjectionReadConsistencyE2ETests.ShouldNeverReturnStaleProjectionOrCrossTenantRowsGivenStandaloneOrSplitHost` covers application-list isolation and boundary-reference exact/non-disclosure in both modes. | Application revision, system-instance and reference list pagination, change preview, and remaining HTTP/MCP permutations. |
| Snapshot read, verification and manifest regeneration | `SnapshotE2ETests.ShouldFreezeExactScopeGivenStandaloneOrSplitWorker` covers cross-tenant verification/manifest and outsider denial in both modes. | Two-tenant snapshot list and cursor, plus full MCP list proof. |
| Platform metadata exception | `OperatorPortfolioE2ETests.ShouldListPlatformTenantMetadataGivenConfiguredOperatorAndHostMode` covers operator-only tenant metadata pagination in both modes. | Keep this separate from client business-record access. |

Still open for the same two-tenant, two-host HTTP/MCP matrix: tenant member,
invitation, self-list and access reads; program list, revision and setup-work
reads; control, commitment and risk draft/history pages; client-service tenant
and program lists/history; boundary program lists, versions, decisions and impact
preview; and the remaining application and snapshot reads above. Existing
capability tests often prove standalone cross-tenant denial or split-host
recovery, but that does not prove every list and cursor in both modes.

The current API has no separate tenant-owned count, export, job, notification
or artifact-content route. Import batch status carries row and invalid counts
plus zero-valued progress fields. Add any future surface to this matrix in the
backend child that introduces it, including restricted-row filtering before
counting and pagination.
