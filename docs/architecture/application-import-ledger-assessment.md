# Application import ledger: Portia ownership assessment

Recorded 2026-10-06 for [EN-05 #195](https://github.com/bdgrz/compliance/issues/195).
This satisfies the platform assessment required by
[ADR 0005](decisions/0005-import-reconciliation-and-background-processing.md#barrier-design).
The acceptance, effect execution, and visibility behavior described below remain
implementation work under #195 and its first consumer #213.

## Evidence and ownership

Compliance pins Portia 0.7.0 in `Directory.Packages.props`. Its installed package
metadata identifies source commit
`20cac664d58c86014c88d3e91203edec7e30b47e`.

| Existing primitive | Evidence at the pinned source commit | Import responsibility |
| --- | --- | --- |
| One aggregate execution with one commit/discard decision and authoritative optimistic concurrency | [IAggregateExecutor](https://github.com/cntryl/portia/blob/20cac664d58c86014c88d3e91203edec7e30b47e/src/Portia.Abstractions/Aggregates/IAggregateExecutor.cs) | Serialize a source's acceptance, cancellation, failure, and commit in one application ledger stream. |
| Read-only aggregate hydration from its own event stream | [IAggregateReader](https://github.com/cntryl/portia/blob/20cac664d58c86014c88d3e91203edec7e30b47e/src/Portia.Abstractions/Aggregates/IAggregateReader.cs) | Read authoritative batch and ledger state in authorizers, handlers, and visibility decisions. |
| At-least-once cross-aggregate reactions with application-owned idempotent effects | [Portia scope](https://github.com/cntryl/portia/blob/20cac664d58c86014c88d3e91203edec7e30b47e/docs/scope.md) | Resume the frozen application plan after interruption; use deterministic batch/row effect identities. |
| Projection data and checkpoint share a commit; reactor effects have a separate checkpoint | [Projectors and reactors](https://github.com/cntryl/portia/blob/20cac664d58c86014c88d3e91203edec7e30b47e/docs/projectors-and-reactors.md) | Maintain import progress and inventory reads without treating a checkpoint as batch acceptance. |

Portia owns event persistence, aggregate execution, optimistic concurrency,
generated registration/serialization, request dispatch, and workload execution.
Its published scope explicitly limits atomic event-store writes to one stream.

Compliance owns the application import decisions: the tenant-declared source
tuple, row correlation, frozen inventory target plan, source claims, proposed
retirements, approver/submitter attribution, per-batch revision, and whether an
imported application effect is usable in a governed operation. These decisions
belong in an `ApplicationImportLedger` and the application workflows that consume
it, composed using the existing Portia primitives.

This implementation requires no new Portia primitive. Any extracted reusable
execution coordinator, generic pending-effect storage, or generic visibility
framework belongs in Portia and needs an upstream proposal before implementation.
The Compliance feature should keep its records and policies specific to application
imports rather than introducing such infrastructure locally.

## Required lifecycle boundary

Current staging stores immutable raw observations in `ImportBatch`. Current
pre-acceptance cancellation also writes that aggregate. Adding acceptance in a
different ledger while retaining that cancellation write would create two
serialization points: both operations could succeed against different streams.

Before exposing acceptance, put every new post-staging transition, including
pre-acceptance cancellation, on the source ledger. The ledger serializes one
accepting batch for each exact tenant/source-key/namespace tuple. Historical
`ApplicationImportCanceled` events remain readable and terminal. Existing batch
IDs, immutable raw content, and per-batch revision semantics must survive this
transition.

Batch/progress reads, cancellation, projection freshness, and idempotent staging
replies must use the authoritative lifecycle assembled from immutable batch data
and the ledger. A projection alone cannot authorize acceptance, cancellation,
effect execution, or commit.

## Delivery gates

The internal initial-observation plan foundation now prepares every valid,
explicitly correlated staged row and freezes it on the source ledger. A header,
one bounded event per planned row, and a SHA-256 seal are raised in one aggregate
outcome; the existing Portia stream transaction persists them together. Each
record advances the batch revision. Replay publishes a frozen plan only after
the row count and hash match, preserves submitter/approver attribution, and
rejects incompatible source or revision histories. Only one batch per source
can be accepting; cancellation releases that batch and retains its plan history.
Linked targets are rechecked before freezing, and correlation edits cannot
change a frozen plan. Import projection freshness includes these plan records.

The dedicated internal `ApplyApplicationImportEffect` command now checks the
trusted Portia system actor and the exact tenant/source/batch/row/target in an
active sealed plan. Its handler reloads authority, then the target aggregate
records one bounded pending audit effect with frozen content and both human
attributions plus a named system process. Target-stream optimistic concurrency
protects replay and current linked-target revision checks. A pending new target
reserves its deterministic identity against manual declaration. Pending effects
do not create active inventory, advance governed revisions, overwrite manual
fields, or appear in inventory/history projections. Cancellation after an
authority snapshot may leave a late durable effect, which remains invisible;
the canceled source rejects subsequent retries. The existing per-stream
persistence boundary cannot prevent that cross-stream race and does not need to
for the visibility contract.

The ledger's `VerifyPendingEffects` gate checks the durable staging stream, the
physical source-stream version of the frozen seal, and every exact target
effect's committed stream position. A raised but unsaved record never counts as
durable. The physical seal version is separate from a batch's lifecycle revision
because older batches share the same source stream. Verification requires every
row, matching frozen payload/attribution/hash, and unchanged governed target
state; it returns a read-only complete proof or a conflict without raising an
event. Missing durability is transient. Canceled batches, stale revision,
foreign targets, altered payloads and changed governed targets fail the gate.
This proof alone does not authorize a visibility commit: all governed consumers
must honor the barrier before commit activation is added.

Manual application revision and retirement now reserve a target while any import
effect remains unsettled. Handlers capture durable canceled batches from each
exact source ledger, then the application executor hydrates the guarded target
again. A newly arrived effect absent from that settlement snapshot blocks the
operation with a transient conflict. An effect appended after target hydration
conflicts with the manual append through the existing target-stream OCC. Only a
durable cancellation releases its matching reservation; an in-memory cancellation
or another source's cancellation cannot release it. Canceled effect records
remain invisible audit data and do not advance the governed revision. Target
reads keep their existing governed state. Commit settlement and all governed
consumer visibility still need implementation before acceptance activation.

No public acceptance route, commit marker, or worker is exposed yet. Committed
source claims, changed/missing-row reconciliation, retirement impact, complete
governed visibility after commit, and public acceptance remain required work.
The eventual commit command must verify every persisted effect and every
relevant governed target before committing. Future live effect recovery,
cancellation races, isolation, and transport framing are separate integration
tasks #801–#804; current acceptance remains unit-only.

1. **Ledger lifecycle and compatibility.** Prove source/tenant identity, staged
   cancellation, historical cancellation replay, expected revisions, per-source
   exclusivity, terminal-state replay, and monotonic per-batch revisions with
   focused unit tests. Update existing read and cancellation paths together.
2. **Authoritative preview and frozen plan.** Classify claims and missing rows
   from the source ledger. Reject invalid, duplicate, and unresolved rows. Record
   explicit correlation choices and retirement impact before accepting the whole
   batch. Keep event payloads within Fitz's documented frame and current staging
   bounds; measure serialized plans and split records within one ledger commit
   where necessary.
3. **Pending effects and visibility.** Attribute and authorize each dedicated
   internal effect against its exact persisted plan, tenant, batch, row, and
   target. Cover retries, missing effects, permanent failures, cancellation races,
   and manual target changes in unit tests. Commit only after every effect is
   authoritatively durable.
4. **Every governed consumer.** Cover get/list/count/history/instance reads,
   validators, reference writes, and readiness/export consumers. Pending and
   rolled-back effects stay invisible. A committed marker with incomplete target
   projections must produce an explicit transient result or an authoritative
   complete read, never a partially visible batch. Apply one consistent source
   ledger state within each read and account for paging across source changes.
5. **Public whole-batch acceptance.** Expose the personal HTTP-only decision only
   after these gates hold. Keep Contributor staging separate from Lead/Admin
   acceptance and cancellation. Preserve attributable decisions and declared-
   complete versus partial source semantics.

Each implementation PR needs focused red/green tests, the full backend unit
suite, review, format/build, and final-head CI. Full host recovery and transport
integration are tracked separately in the future E2E milestone, including
[#657](https://github.com/bdgrz/compliance/issues/657) and
[#658](https://github.com/bdgrz/compliance/issues/658). This assessment supplies
ownership and sequencing evidence; it is not proof that batch acceptance works.
