# Event-sourced history and effective versions

Status: proposed for M0-A01. The existing tenant and program flows prove the
storage shape in standalone and split API/worker hosts. Operational recovery
targets still need an owner and a restore exercise before this ADR is accepted.

Decision owner: tech lead and product owner. Date: 2026-09-19.

## Decision

- Fitz streams are the authoritative business history. A Portia aggregate owns
  one stream and applies its ordered events. A command commits to one stream
  with optimistic stream concurrency. Multi-stream changes use an attributable,
  replay-safe reactor or a workflow that exposes incomplete progress; they do
  not claim one transaction across streams.
- Each tenant-owned stream uses the immutable organization UUID as its Fitz
  realm. Platform records use a separate, named realm and may refer to client
  identities without storing client-owned business records there. Projections
  begin reads in the tenant realm; authorization checks membership and the
  current tenant lifecycle before business reads. A client export or deletion
  process must enumerate all of that client's streams and projections; a
  single database-level tenant switch is not available.
- Stable opaque UUIDs identify entities and versions. A logical record keeps
  one stable identity; every accepted version has its own immutable version
  identity and monotonic revision. Draft edits require an expected revision.
  A stale edit reports a conflict and the current revision so a client can
  reload. An approved version is never edited or deleted. A successor is a new
  proposal; its approval closes the previous effective interval without
  changing the previous version's content.
- Store occurred time, effective time or interval, covered period, and
  observed or imported time as distinct fields when the workflow needs them.
  Events record the actor or source and the time captured by the service. An
  effective-date query selects an approved version whose half-open interval
  contains the requested date. Overlapping approved intervals for one logical
  record are rejected by the owning aggregate.
- Fitz KV read models and Portia projectors serve lists, search, and historical
  version queries. Projection data and its checkpoint commit atomically. A
  read model can lag its source stream, so a command's invariant uses the
  aggregate rather than a projection. A query that promises a minimum version
  must wait for or report projection lag; it must not silently return an older
  version as current.
- Stream events and projection schemas are versioned separately. New event
  readers retain support for all stored event types or run a verified
  migration before deployment. A projection change creates a new projection
  identity and replays from the retained event history; it does not reinterpret
  an old checkpoint with a new schema. Aggregate streams remain bounded by
  domain identity and explicit rollover where needed; Portia does not support
  aggregate snapshots or prefix truncation.

## Why this storage shape

The application already uses Portia aggregates and Fitz event streams for
tenants, invitations, permissions, and programs. `ComplianceProgram` keeps a
tenant realm and checks an expected revision. `ProgramDirectory` writes both
current and historical views through a Fitz KV projection store. The split-host
broker tests run an API and worker independently. Keeping this model avoids a
second authoritative write path while preserving the history required by the
domain model.

A relational temporal primary store could make cross-record constraints and
ad hoc joins easier, but it would require a second transaction and event
publication design. A hybrid authoritative model would create ambiguity about
which store wins during recovery. Read models can still move to another store
when that store can meet the projector checkpoint contract.

## Recovery and operational gates

Fitz event streams are required to rebuild business projections. Back up the
durable event store and any non-rebuildable integration state together with
the deployed event-reader version. Test restoring them into an isolated broker,
then replay projections and compare record counts and selected histories with
the source. A projection-only backup is insufficient. Do not delete historical
stream prefixes: Portia hydration requires contiguous physical offsets.

The deployment owner must set numerical RPO and RTO targets and prove them with
a timed restore exercise before this ADR is accepted for production. This
repository does not establish those targets or demonstrate per-organization
physical restore. Until then, per-organization export and deletion are
application workflows requiring an inventory of all tenant-owned streams,
projection data, artifact objects, and retained backups.

## Consequences for EN-02 and R1-02

EN-02 supplies the reusable version, interval, conflict, and impact-preview
contracts. R1-02 owns boundary lifecycle and its approval rules; it must bind a
decision to the exact version and recheck impact before approval. R1-02 may
start with unresolved scope references for inventories that are not yet
governed. Service references resolve to active records in the boundary's
program. Other references gain governed inventory relationships through a
reviewed successor version. An engagement
stores the approved boundary version ID it used. The first boundary tests must
exercise replay, stale revision, overlapping intervals, lag, and standalone
and split-host parity.

The first boundary implementation accepts only explicit unresolved scope
references until the owning inventories can validate governed record IDs. Its
successor impact preview names contexts that have no contributor yet, and
approval refuses an incomplete preview. The approval decision stores the
acknowledged preview digest; this does not make unrelated context writes atomic
with the boundary stream, so later contributors must define their own lag and
stability guarantees before the boundary backend child is complete.

Boundary reads expose a stream revision independent of each draft version's
revision. `minimum_revision` on the current boundary read distinguishes an
unreached source revision from projection lag after authorization. The
`BoundaryDirectoryV2` projection uses a new Fitz store URI and checkpoint
identity so retained boundary events rebuild that revision from the start;
existing `BoundaryDirectory` checkpoints cannot be reused with this schema.
Until the new projector catches up, revision-aware reads report a conflict and
ordinary reads may show no boundary. The previous projection remains available
for rollback and can be removed after replay and operational readback.
