# Occurrence population pagination acceptance

Refs #278. This test-only increment covers the actual public `ListControlOccurrences` request through production `AddCompliance` composition. It does not change the live offset cursor contract.

A natively approved weekly plan yields seventeen deterministic occurrence identities through a fixed UTC clock's 90-day horizon. The test reads two rows per page using each returned `NextCursor`. After reading the first page, the owner records a personal HTTP `not_applicable` attestation for an already-returned expected occurrence. The remaining pages contain all original identities in their original due-date/identity order, without omissions or duplicates, and terminate with a null cursor.

A fresh complete traversal exposes the submitted occurrence once at the same identity, period, due date, plan and control version. New Fitz KV projections rebuilt from the retained event store reproduce the same public population state and one independent review item; repeated catch-up preserves its identity and counts.

The proof fixes the clock and leaves cadence, source control versions, filters and horizon unchanged. It does not promise a frozen cursor population when those inputs change. State-filtered populations can change on submission; immutable cross-request paging and broader population reconciliation are outside this increment.

Native plan/control setup uses `OperationsFixture`; actor/grant, resource-scope, tenant-activity and membership-directory doubles are explicit scaffolding. The test uses production request handlers and registered tenant projectors, an in-memory retained source store and fresh KV resources. This is backend unit acceptance, not a broker, host or client test. Governed evidence resolution and exact evidence content versions remain under #196/#272, and the partial parent stays open. Existing behavior may pass the focused proof immediately; no production RED-to-GREEN change is claimed.
