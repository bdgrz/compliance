# Read models, projections, and as-of calculations

Status: proposed technical default for M0-A05, 2026-09-22. It records the
tenant-scoped projection contract already exercised by Program, Boundary, draft
history, and application boundary-reference reads. M0-A05 remains open pending
accepted M0-A01 recovery prerequisites, a dedicated projection-derived-read
spike in standalone and split hosts, and product-owned calculation and
cross-client rules.

Decision owner: tech lead and product owner.

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

[PR #321](https://github.com/bdgrz/compliance/pull/321) adds separate retained-
source recovery evidence: a fresh broker and fresh hosts restore Program source
state and replay the production tenant-scoped `ProgramDirectoryProjector` in
standalone and split API/worker modes. That proof does not establish portable
backup or restore, numerical recovery targets, a global calculation snapshot,
or cross-client authorization.

## Alternatives and limits

Synchronous projection writes would blur source authority, prevent independent
replay, and make host deployment topology part of command correctness. A
timestamp-only `as_of` label would make an unreproducible mixed read appear
historical. Reusing a checkpoint after changing a projection schema would
misrepresent what the checkpoint covers. A central cross-client read model
would disclose restricted client data unless a separately reviewed authority
model filters it before counts, pagination, search, and drill-down.

R1-08 owns readiness inputs, rule semantics, materiality, unknown and draft
treatment, gaps, and historical assessment retention. M0-D15 and R2-11 own
work-source mapping, due and blocked semantics, prioritization, reminders,
assignment, and escalation. M0-D25, M0-D26, and F1-02 own firm-staff access,
independence boundaries, and any cross-client portfolio authority. M0-A04 owns
field-level restriction policy. M0-A01 still needs an accepted operational
owner, portable backup-and-restore exercise, RPO/RTO targets, and a per-
organization recovery, export, and deletion procedure.
