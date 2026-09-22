# Read models, projections, and as-of calculations

Status: accepted for M0-A05, 2026-09-22. It records the tenant-scoped
projection contract already exercised by Program, Boundary, draft history, and
application boundary-reference reads, and the dedicated projection-derived-read
spike that proves it in standalone and split API/worker hosts. M0-A01 is
accepted; its whole-platform recovery delivery remains a separate Portia/DevOps
gate.

Decision owner: Jeff Repanich, tech lead, 2026-09-22. Product-owned
calculation semantics and any cross-client authority remain with the owners
named under [Deferred ownership](#deferred-ownership).

## Options considered

| Option | Outcome |
| --- | --- |
| Synchronous projection writes inside the command transaction | Rejected. It blurs source authority, prevents independent replay, and makes host topology part of command correctness. |
| Asynchronous tenant-scoped Portia projectors with transactional checkpoints | **Chosen.** Source streams stay authoritative; projections are rebuildable and host-mode independent. |
| Serve the last successful projection with a staleness marker when a derived read lags | Rejected for reads that declare source anchors. A stale success is indistinguishable from a current answer to an API or MCP client. The read returns a retryable transient conflict instead. |
| Timestamp-only `as_of` labels on live queries | Rejected. A live multi-record read is not reproducible from a timestamp. |
| A shared cross-client read model for portfolio queries | Rejected. It copies restricted client rows outside their tenant before any authority model filters counts, pagination, search, or drill-down. |

## Decision

Portia aggregates and Fitz event streams remain authoritative for command
invariants and business history. Read models are asynchronous Portia projectors
that write tenant-scoped Fitz KV data. A projector runs through the supported
tenant workload path and commits its projection rows and its checkpoint in one
transaction. A failed batch makes neither visible; retry and retained-event
replay rebuild the same projection.

Every projector has a named component identity, event pattern, and projection
resource identity. A schema or interpretation change receives a new identity
and replays retained source events into a new resource. It does not reinterpret
or reuse the older checkpoint. A cutover keeps the prior projection readable
until the new projection is reconciled and authorized consumers have moved.

An authorized read may be eventually consistent only when its contract does
not promise a source revision. A read that accepts an explicit source revision
anchor returns a transient conflict if the source has not reached that revision
or if its projection has not caught up. It must not return an older successful
view. A derived read declares the source patterns or record anchors it checks;
it cannot claim unrelated records or a whole tenant are current merely because
one projector checkpoint has caught up.

Authorization happens before a command or query reads a source aggregate or a
projection. Tenant-scoped projection storage uses the tenant realm, and every
returned projection row is checked against the request tenant and requested
resource before disclosure. A caller without access receives the same
not-found result used for an absent resource and cannot infer rows, counts,
watermarks, or failure details. A cross-client projection or portfolio must use
its own explicit authority model; it must not copy client rows into a shared
read model merely to make aggregate queries convenient.

Cross-client reads are denied by default. A platform user who holds
memberships in several client organizations reads each organization only
through that organization's route and authorization. Presenting one
organization's identifier under another organization's route returns the
not-found result used for an absent resource, and the other organization's
lists do not contain the record. Any later cross-client portfolio or work
queue (F1-02, F1-05) must be authorized under M0-A04 with M0-D25 and M0-D26
rules before it may read more than one tenant.

## As-of calculation identity

A timestamp alone does not make a live multi-record calculation reproducible.
A future `ReadinessAssessment` or work calculation must retain its tenant and
scope, rule-definition identity and version, exact immutable input identities
and content/version references, calculation time, and terminal calculation
state. Its result can then distinguish a completed calculation from incomplete
inputs and a failed calculation without treating any of those states as a
successful assessment.

A live query cannot call itself a coherent historical result solely because it
has an `as_of` timestamp. A reproducible cross-record result needs an immutable
snapshot or another retained exact input set. Direct projection lag remains a
retryable transport conflict. Incomplete inputs and a failed calculation remain
explicit domain results when the owning calculation exists. This ADR does not
add a generic calculation-status API before a readiness or work-calculation
domain owns those terms and their retention rules.

## Thin evidence

`ProjectionReadConsistencyE2ETests.ShouldNeverReturnStaleProjectionOrCrossTenantRowsGivenStandaloneOrSplitHost`
is the dedicated M0-A05 spike, run once in the cohosted standalone host and
once in split API/worker hosts against the real broker. After a boundary write
that references an application, every application boundary-reference read is
either a `409` with `Portia-Transient: true` or a `200` page that contains the
new boundary; no older successful view is ever returned, and the read
recovers once the projection catches up. The split run stops the worker before
the write, deterministically observes the transient conflict, then restarts a
fresh worker and reads the projected row. The standalone run exercises the same
never-stale invariant with its cohosted projector; it cannot pause that
projector, so it does not deterministically force the conflict. In both modes
one user who administers two client organizations receives `404` when reading
organization A's application through organization B's HTTP route and MCP tool,
and B's application list stays empty. An outsider receives the same HTTP
not-found problem as for an unknown organization, and the MCP tool fails with
`NotFound`.

`ApplicationBoundaryReferencesV1` is a tenant-scoped reverse projection over
the boundary event pattern. Its read-consistency guard compares that exact
pattern with the projection checkpoint before returning even an empty page.
When an event is pending, it returns a transient Portia conflict. The
application boundary-reference HTTP operation and its read-only MCP tool expose
the same retryable source-cursor catch-up contract. This endpoint does not
accept a caller-supplied record revision anchor. Its source scan and projection
list are separate operations, so an ordinary concurrent-write window remains.

`ApplicationBoundaryReferenceDirectoryTests.ShouldReportLagThenAllowEmptyReadGivenSourceCheckpointCatchup`
proves source cursor lag and recovery after catch-up.
`ApplicationInventoryE2ETests.ShouldProjectDeclarationsGivenSplitApiAndRestartedWorker`
stops an independently hosted worker, observes the transient boundary-reference
read through HTTP and MCP, then restarts the supported Portia worker and reads
the projected rows. This retry-and-recovery proof is split-host only.
`ShouldDeclareApplicationAndSystemInstanceGivenAuthorizedBrokerHost` proves
the same read is undisclosed to an outsider over HTTP and MCP in the cohosted
standalone host; it does not prove standalone lag and recovery.

`ApplicationControlDraftReferencesV1` applies the same tenant-scoped
source-cursor rule to current Control-draft applicability. It is a separate
fresh projection and checkpoint, so an already-caught-up Control current-read
projection cannot falsely certify a reverse-reference backfill. Application
change preview returns a transient conflict while either its boundary or
Control reference source is ahead. It returns only direct Application draft
references and labels system-instance references plus approved-version and
lifecycle impact as pending; it does not claim a cross-stream snapshot.

[PR #321](https://github.com/bdgrz/compliance/pull/321) adds separate retained-
source recovery evidence: a fresh broker and fresh hosts restore Program source
state and replay the production tenant-scoped `ProgramDirectoryProjector` in
standalone and split API/worker modes. The later portable-local-volume probe
extends that evidence through source-volume loss and restore into a fresh local
volume. Neither probe establishes production backup or restore controls,
measured achievement of the 15-minute RPO or 4-hour RTO, a global calculation
snapshot, or cross-client authorization.

## Deferred ownership

Reusing a checkpoint after changing a projection schema would misrepresent
what the checkpoint covers, so a changed projector always receives a new
identity and replays into a new resource.

This decision fixes the transport-level states: projection lag is a retryable
transient conflict, and a missing or unauthorized resource is an
indistinguishable not-found. How a browser presents a retrying read, and the
domain meaning of incomplete inputs or a failed calculation, belong to the
owners below rather than to this ADR.

R1-08 owns readiness inputs, rule semantics, materiality, unknown and draft
treatment, gaps, and historical assessment retention. M0-D15 and R2-11 own
work-source mapping, due and blocked semantics, prioritization, reminders,
assignment, and escalation. M0-D25, M0-D26, and F1-02 own firm-staff access,
independence boundaries, and any cross-client portfolio authority. M0-A04 owns
field-level restriction policy. M0-A01 accepts the event-history and recovery
boundary; whole-platform recovery implementation and timed operational evidence
remain with [cntryl/portia#70](https://github.com/cntryl/portia/issues/70) and
DevOps. That external gate neither defines projection policy nor requires a
physical per-tenant restore.

## Consequences

- Every new projection-derived read declares the source patterns or record
  anchors it checks and returns a transient conflict, not a stale success,
  while they are ahead of its checkpoint.
- Every tenant-scoped read authorizes before touching a source aggregate or
  projection and checks returned rows against the request tenant.
- Projection schema changes ship as new projector identities with replay and a
  reconciled cutover.
- Historical or reproducible results require an owning calculation record with
  exact immutable inputs; a generic calculation-status API is not added ahead
  of that domain.
- Cross-client reads stay unavailable until an A04-governed authority model
  for F1 exists.
