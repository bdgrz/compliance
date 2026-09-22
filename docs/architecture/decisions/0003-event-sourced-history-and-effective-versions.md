# Event-sourced history and effective versions

Status: accepted for M0-A01 application architecture, 2026-09-22. The existing
tenant and program flows prove the storage shape in standalone and split
API/worker hosts. A controlled local restart and isolated projection replay
prove the retained-source recovery path. A portable local-volume
archive-to-fresh-volume restore proves a bounded local-volume-loss path. Those
probes are regression evidence, not a production recovery claim. Whole-platform
recovery delivery and its timed operational proof are external platform gates
owned by [cntryl/portia#70](https://github.com/cntryl/portia/issues/70) and
DevOps; they do not keep this application-domain decision proposed.

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
  The interval end is derived from the next approved version's effective start;
  a successor never changes its predecessor's immutable version payload or a
  frozen snapshot's content identity. The boundary projector also rejects an
  overlapping approval during replay, rolling back that projection batch.
- Fitz KV read models and Portia projectors serve lists, search, and historical
  version queries. Projection data and its checkpoint commit atomically. A
  read model can lag its source stream, so a command's invariant uses the
  aggregate rather than a projection. A query that promises a minimum version
  must wait for or report projection lag; it must not silently return an older
  version as current.
  Derived program setup-work reads accept a minimum program revision and,
  when a caller has just changed a boundary, that boundary's ID and minimum
  revision. They report source or projection lag before returning derived work.
  These expectations do not create a global snapshot across every boundary in
  a program; each changed boundary needs its own freshness check.
- Stream events and projection schemas are versioned separately. New event
  readers retain support for all stored event types or run a verified
  migration before deployment. A projection change creates a new projection
  identity and replays from the retained event history; it does not reinterpret
  an old checkpoint with a new schema. Aggregate streams remain bounded by
  domain identity and explicit rollover where needed; Portia does not support
  aggregate snapshots or prefix truncation.

## Current draft-history slice evidence

The control, commitment, and risk draft history slice applies that projection
rule with one isolated V1 Fitz read model per draft family. The resources are
`kv://bdgrz/control-draft-history-v1/projection`,
`kv://bdgrz/commitment-draft-history-v1/projection`, and
`kv://bdgrz/risk-draft-history-v1/projection`. Each has its own checkpoint and
projector identity, replays retained tenant events from the beginning, and
writes an immutable row for every creation or revision under a per-draft,
monotonically ordered revision index. A revision event carries the new mutable
content; the projector reads its predecessor V1 row to retain creation-only
metadata in later history rows.

The authorized history list hydrates the requested aggregate in the tenant
realm, verifies the requested program and draft identity, and validates any
minimum source revision against that authoritative aggregate. It then requires
the V1 history row at the aggregate's current source revision before returning
any page. A missing row is a retryable projection-lag conflict, rather than a
partial history result. The handler also validates every returned row's tenant,
program, and draft scope. This is a per-record source-revision gate; it does
not claim a global snapshot across unrelated draft families.

Current-directory evolution follows the same rule. `ControlDraftDirectoryV2`
replaces the original current Control projection for new deployments with the
separate `kv://bdgrz/control-draft-directory-v2/projection` resource and a new
checkpoint; it replays retained Control events rather than reinterpreting the
old current-directory checkpoint. The V1 history projectors remain separate,
so their historical rows and checkpoints are not repurposed for current-read
semantics.

When a new event discriminator cannot be read by a prior binary, its writer
starts disabled behind an application release gate. Deploy the new API and
workers as readers, drain every prior reader, and only then enable the writer.
That readers-before-writers sequence keeps a rolling overlap from placing an
unknown event in a stream still read by an older process. Once the event is
written, a rollback keeps a reader build that understands its discriminator.

## Concurrency evidence

The real-broker Compliance acceptance test holds a real append session on the
exact program stream before it dispatches a revision. That revision is rejected
at stream session admission with `Cntryl.Fitz.StreamException` domain code
`2002`, `StreamSessionAlreadyActive`, before it can attempt a stale append. This
is the broker's intended per-resource append-session contention response. Portia
0.5.5 translates that admission response into `EventStreamConcurrencyException`
before a session exists, while retaining the underlying Fitz exception. The
real-broker contract proves a safe transient HTTP 409 and a structured transient
MCP Conflict in both standalone and split API/worker coverage. After the held
session rolls back, a normal revision succeeds and exactly one revised event is
durable.

[cntryl/portia#60](https://github.com/cntryl/portia/issues/60) covers the
stale-append path and [cntryl/portia#65](https://github.com/cntryl/portia/issues/65)
closed the `2002` session-admission path. No application catch or automatic
retry has been added. The application-domain M0-A01 decision is accepted;
whole-platform recovery delivery remains the external Portia/DevOps gate
described below.

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

`ProgramRecoveryE2ETests` supplies two complementary thin recovery probes. It
runs Fitz in local storage mode with one Compose-project-scoped volume mounted
at `/data`. Each probe writes two tenants and a revised program, then records
the authorized current HTTP result, immutable history, and exact source event
count. Both standalone and split API/worker cases prove the same current
result, history, source count, and a cross-tenant not-found response.

The retained-source probe gracefully stops and removes the broker container
without removing that volume, then starts a fresh broker container against it.
The portable-local-volume probe gracefully stops the source broker, streams
`/data` to a temporary Docker tar with `docker container cp --archive`, hashes
that tar, and writes an adjacent manifest containing the source broker image
and `ProgramDirectoryProjector` event-reader assembly informational version
under test. It destroys the source Compose
project with `down --volumes`, verifies that the source volume no longer
exists, creates a never-started broker in a distinct GUID-scoped Compose
project, imports the tar into its empty `/data`, and only then starts it. The
temporary tar and manifest are test artifacts deleted after the probe; they are
evidence for a controlled local volume copy, not an operational backup product.

The test then starts a miniature Portia worker with the restored real
`IEventStore`, a fresh `InMemoryKvClient`, and only the restored tenant. It
registers the production `ProgramDirectoryProjector` through
`AddProjector(..., WorkloadScope.PerTenant)`, so Portia owns tenant binding,
checkpointing, and replay. The resulting isolated current and revision rows
must match the HTTP source representation. The fresh KV client is deliberate:
using a rebuild ID alone changes a checkpoint identity, not the live projection
data route.

The retained-source probe proves that commit-visible Fitz Stream source state
survives a controlled fresh-container restart against retained local storage.
The portable-local-volume probe additionally proves that the same source can
be restored from a Docker tar after intentional source-volume loss into a
distinct fresh local volume. In both cases, fresh application workers can
reconstruct the authorized program projection from that source. The isolated
replay confirms the same production projector can rebuild its current and
revision rows from the restored stream. These probes do not prove preserved
live KV or checkpoint rows, a production backup export or restore process,
non-rebuildable integration-state coverage, cloud-provider recovery,
broker-upgrade compatibility, measured achievement of the 15-minute RPO or
4-hour RTO, or a physical per-organization restore.

Fitz event streams are required to rebuild business projections. The upstream
recovery capability must preserve compatible durable event state, declared
non-rebuildable integration state, and the deployed event-reader version. Its
clean-environment restore must replay projections and compare record counts and
selected histories with the source; a projection-only backup is insufficient.
Do not delete historical stream prefixes: Portia hydration requires contiguous
physical offsets. Compliance declares the required business inventory and
acceptance criteria, while Portia/Fitz implements the mechanism.

## Whole-platform recovery boundary

The application decision is to recover the whole platform, not independently
recoverable physical tenant partitions. The production targets are a 15-minute
RPO and a 4-hour RTO. DevOps owns the runbooks, on-call restore execution, and
recurring timed recovery drills. Portia/Fitz owns the provider-neutral backup,
restore, integrity-verification, and recovery-evidence plumbing in
[cntryl/portia#70](https://github.com/cntryl/portia/issues/70). Compliance does
not add provider SDK integrations, backup scripts, or restore orchestration.

Compliance declares the business recovery inventory and acceptance criteria:
durable Portia/Fitz event state; every tenant artifact bucket, where artifact
storage provisions one, and its immutable objects; tenant bucket and
encryption-key-reference mappings; and external integration state that cannot
be replayed from events. Secrets are restored from their authoritative
secret-management source and are not copied into backups.
Per-tenant export and deletion remain explicit application workflows that
inventory the tenant-owned records and artifacts; physical per-tenant restore is
out of scope.

This ADR does not claim production recovery readiness. That requires delivery
of the upstream capability and a DevOps timed production-like restore that
measures the targets above. The open platform gate does not alter the accepted
Compliance persistence, versioning, tenancy, or projection decision, and it
does not justify recovery plumbing in this repository.

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

Application and system-instance reverse boundary references use their own
`ApplicationBoundaryReferencesV1` per-tenant projector, checkpoint, and Fitz
resource. The projector replays retained boundary events from the beginning
without changing `BoundaryDirectoryV2`. It indexes the current draft and each
approved boundary version by exact governed record ID. Revising or discarding
a draft replaces or removes its prior reference rows; the reverse query is not
a complete history of draft revisions. Approved-version rows remain and are
marked historical when a successor is approved. The boundary event stream is
the source for earlier draft revisions. An empty reverse query is returned only
after the projector checkpoint has reached the boundary-area event source at
the time of the query; otherwise it returns a retryable conflict.
An application query returns direct application references. A concrete
system-instance reference is queried through that instance's route because a
boundary scope entry does not carry the instance's owning application ID.

Current Control-draft applicability uses a separate
`ApplicationControlDraftReferencesV1` per-tenant projector and
`kv://bdgrz/application-control-draft-references-v1/projection` resource. It
replays retained Control events from the beginning rather than adding rows to
`ControlDraftDirectoryV2`, whose existing checkpoint cannot establish that a
new reverse index has been backfilled. The projection replaces indexed current
entries on a revision and removes them on a discard; legacy events without
applicability yield no rows. Application change preview requires the Control
area checkpoint to catch up and returns only direct Application entries. It is
therefore advisory across separate Application, boundary, and Control streams,
not a complete or atomic control-lifecycle calculation.
