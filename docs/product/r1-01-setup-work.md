# R1-01 program setup work: backend slice

The read-only `GET /api/v1/tenants/{tenant_id}/programs/{program_id}/setup-work`
and `bdgrz.program.setup-work.get` MCP tool derive work from the current
program and its projected boundary records. The response carries the program
revision, actionable work items, and `next_boundary_cursor` for a bounded scan.
Clients pass `boundary_limit` and `boundary_cursor` to page through boundaries.
A missing program is
not-found, and tenant access follows the same authorization as program reads.

The current derived items identify a missing system boundary, a boundary that
has no approved version, a boundary without an included client service, and
explicit unresolved scope references. They point
to the owning program or boundary instead of storing another completion flag.
The query fences both input projections. It captures the live full-tenant
`ProgramDirectory` and `BoundaryDirectoryV2` checkpoints, checks each stored
source cursor, derives the page, then requires unchanged checkpoints and checks
the cursors again. Pending source events or checkpoint movement return a
retryable HTTP `409` with `Portia-Transient: true`, or a transient MCP conflict,
instead of a stale page. The full-tenant check may require a retry for an
unrelated tenant event. A write after the final check remains an ordinary
concurrent-write window; this live query is not an immutable snapshot.

An empty page does not claim the program is ready for assessment: criteria,
controls, risks, and evidence workflows are still being delivered. Optional
program target dates, advisor, and audit firm fields do not become required
setup blockers merely because they are blank.
