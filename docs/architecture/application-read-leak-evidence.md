# Application read isolation evidence

Issue #157 tracks tenant-owned read isolation across the backend, HTTP API, and MCP. `ApplicationReadLeakMatrixE2ETests` supplies a two-populated-tenant broker case in standalone and split API/worker hosts. One signed-in member owns both tenants, and an unrelated signed-in member exercises denial.

The test creates a distinct application and two system instances per tenant, plus two current draft boundary references for the application and one instance in each tenant. It verifies positive exact application-revision and system-instance reads over HTTP and MCP. It follows every page of each tenant's application-revision, system-instance, application-boundary-reference, and system-instance-boundary-reference lists with `limit=1` over both transports, checking tenant and record IDs against each tenant's own seeded set. It also carries an HTTP cursor from tenant A to tenant B's corresponding list and accepts only a rejected cursor or tenant B rows.

For each list and exact read, a foreign application or instance ID under the other tenant returns `NotFound`; the unrelated member is denied. Application-change previews for both tenants return only their own current draft application boundary references, while a foreign application ID and the unrelated member are denied over HTTP and MCP.

This evidence covers current draft boundary references and the populated preview `boundary_references` field. It does not seed approved boundary versions, control draft references, or other preview contexts. It does not transplant MCP cursors between tenants, exercise a stopped worker or replay, or prove the top-level application list. PR #369 carries the broader #157 inventory; reconcile these limits there after both branches land.
