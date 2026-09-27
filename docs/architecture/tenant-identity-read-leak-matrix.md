# Tenant identity read isolation evidence

[EN-01 backend #157](https://github.com/bdgrz/compliance/issues/157) requires
standalone and split API/worker proof for each implemented tenant-owned read.
`TenantIdentityReadLeakMatrixE2ETests` runs the same two-tenant broker scenario in
both host modes. It creates unrelated verified owners and tenant records, then
uses the platform operator to activate one additional client-personnel member in
each tenant. Owner and operator sessions exercise the reads; separate member
sessions accept the invitations, and an outsider exercises denial. Personal
invitation acceptance is HTTP-only and is outside this read matrix.

| Read family | Current HTTP and MCP proof | Remaining proof |
| --- | --- | --- |
| Tenant detail | Owners see their own tenant; another owner and an outsider receive `NotFound`. A platform operator can read both tenants as the accepted metadata exception. | Other tenant-owned details are inventoried in their own capability matrices. |
| My tenants | Each owner sees only their tenant. The outsider and operator see no memberships. Both transports traverse `limit=1` cursors, including empty filtered pages; unauthenticated HTTP is rejected. | This is a membership view, not a tenant inventory for the operator. |
| Tenant members | The operator sees exactly the two active members of each tenant. Both HTTP and MCP traverse `limit=1` pages to exhaustion, check each tenant ID and member ID, and reject a cursor issued for the other tenant. Tenant owners and an outsider cannot use the operator-only list. | No total-count field is returned. Actor snapshots and later identity replacement/deprovisioning still need lifecycle proof. |
| Tenant invitations | Each owner sees only their own two invitation emails while traversing `limit=1` cursors. Foreign tenant paths fail; filtering the owned tenant with the other tenant's email returns an empty page. A cursor issued in tenant A is rejected in B with HTTP `BadRequest` and MCP `Validation`. | Delivery and personal acceptance have separate lifecycle evidence. |
| Effective member access | Each owner reads their own tenant access. Foreign tenant and foreign user combinations return `NotFound`. | Actor snapshots and later identity replacement/deprovisioning still need lifecycle proof. |

These read contracts return items and continuation cursors, without a total
count field. The scenario waits for projections and invitation authority before
asserting isolation; it does not treat an empty page caused by lag as proof of
filtering. Broader current-surface coverage and actor lifecycle evidence remain
on [#157](https://github.com/bdgrz/compliance/issues/157).
