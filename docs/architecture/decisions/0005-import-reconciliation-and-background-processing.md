# Import reconciliation and background processing

Status: accepted for M0-A06 as a decision record, 2026-09-22. Decision owner:
Jeff Repanich (product owner and tech lead). Date: 2026-09-22.

This ADR records the import semantics that every later import, collection, and
reconciliation must follow. It does **not** deliver them. By product-owner
decision, the implementation and the thin standalone/split API/worker spike
that proves it move to
[EN-05 backend #195](https://github.com/bdgrz/compliance/issues/195), which
keeps every existing acceptance criterion. No import or integration work is
scheduled until the canonical data shape
([M0-D28 #139](https://github.com/bdgrz/compliance/issues/139)) and the
storage internals are settled. The staging and preview slice already on `main`
(PRs #249, #258, #259) is unchanged and makes no Application effect.

## Decision

1. **Staged, never active.** An import records an observation from a named
   tenant source and never mutates governed records while it is staged or
   previewed. The model is: stage, validate, preview, accept or cancel.
2. **Acceptance is all-or-nothing.** A person accepts the whole batch as one
   unit. No imported effect is visible to any governed reader or validator
   until the batch commits. A failed or canceled import leaves no partial
   active record. A batch with an invalid or duplicate row cannot be accepted;
   the submitter corrects the source and stages again. The per-row
   explicit-subset option is rejected.
3. **A durable batch visibility barrier provides the atomicity.** Portia commits
   one stream at a time, so no transaction spans a batch and its many target
   streams. Visibility is decided by a single commit marker instead (see
   [Barrier design](#barrier-design)).
4. **Declared-complete sources propose tombstones.** For a batch with
   `coverage: declared_complete`, previously imported records from the same
   tenant source key and namespace that are missing from the batch are shown in
   the preview as proposed retire or tombstone changes. They apply only when a
   person accepts the batch, under the same all-or-nothing barrier. A `partial`
   batch never implies removal, and nothing retires automatically.
5. **Only the Compliance Lead or Org Admin decides.** Accepting and canceling
   an import require inventory-management authority (the Compliance
   Management and Tenant Administration built-in roles). A Contributor
   (Compliance Participation) may stage an import and read its preview but
   cannot accept or cancel it. Acceptance is a personal decision and stays
   HTTP-only; no MCP tool accepts an import.
6. **Raw rows are retained per M0-D16.** Staged raw rows and rejected-row
   reports are retained for seven years under the
   [M0-D16 #73](https://github.com/bdgrz/compliance/issues/73) evidence
   retention decision. Import storage exposes a retention hook and defines no
   period of its own; hold and disposition follow M0-D16.
7. **Organization context is verified everywhere.** Every HTTP request, MCP
   call, job, reactor message, retry, and progress report carries exactly one
   `tenant_id` and re-verifies it against the authoritative batch before acting.
   A system actor alone is not authority to accept a batch.

## Barrier design

This is the design for EN-05 to build and prove. It is recorded here, not
built.

- **Per-source ledger.** Each tenant source (tenant, source key, namespace) has
  one event-sourced ledger stream. The ledger is the only serialization point
  for that source: it records acceptance intent, cancellation, failure, and the
  commit marker, and it owns committed source-record claims. Acceptance,
  cancellation, and commit are optimistic-concurrency writes to the same
  stream, so a cancellation and a commit can never both succeed. Only one batch
  per source may be accepting at a time; a concurrent acceptance returns a
  transient 409.
- **Acceptance intent.** Accepting records a durable, frozen plan on the ledger:
  each new record with a deterministic target ID derived from the batch and row,
  each existing claim it links, and, for a declared-complete batch, each
  proposed retirement. The plan is computed from the ledger's authoritative
  claims, never from a projection.
- **Pending-under-batch effects.** A tenant-scoped reactor writes each effect to
  its target stream marked with the source and batch that must commit. Effects
  are idempotent by batch and row, so replay after a crash writes nothing new.
- **Commit marker.** After an authoritative read confirms every planned effect
  is durable, one ledger event commits the batch. A missing effect is a
  transient failure that the reactor retries; it never commits a partial batch.
- **Governed reads and validators.** Every governed read (get, list, history,
  instances) and every validator or command that references a target applies
  the barrier. It hides a record whose origin batch has not committed and
  applies a pending retirement only after its batch commits. Commit is
  monotonic, so a check that sees a record as visible cannot be reversed.
- **Recovery.** A crash mid-accept rolls forward: the reactor resumes from its
  checkpoint and replays idempotent effects, then commits. A cancellation
  before commit, or a permanent effect failure, rolls back by recording the
  terminal state on the ledger. Effects already written stay permanently
  invisible because their batch can never commit. Target IDs are per batch,
  so a later batch never collides with rolled-back effects.
- **Batch revision.** A batch's revision spans its staging stream and its
  ledger events, so `expected_batch_revision` and `minimum_revision` keep one
  monotonic meaning.
- **Platform capability.** The barrier (pending-under-batch effects, a commit
  marker, and barrier-aware reads) is generic. EN-05 should assess whether it
  belongs in Portia, following the rule that reusable infrastructure lives
  there, before building a Compliance-local version.

## Staging contract (unchanged)

The first consumer stages bounded, tenant-supplied Application rows as an
original product input. An import is an observation from a named source, not an
authoritative Application, verified owner, classification, or scope decision.
The tenant chooses a stable `source_key` and `source_namespace`, then supplies a
`source_record_id` per row. The source tuple is tenant ID, source key,
namespace, object kind (`application`), and exact source record ID. These are
claims by that tenant; they do not imply connector authentication or rights to
third-party source material. The server records the submitter, submission time,
source tuple, coverage declaration, ordered raw field values, and a
server-computed SHA-256 of canonical input.

A caller supplies a UUID `submission_id` for one intended observation. Within
one tenant, source key, and namespace, repeating that ID with identical
canonical input returns the same batch and state; changing the input returns a
conflict. A new submission ID may repeat the same content and remains a new
observation whose rows can be reported unchanged. A batch ID is deterministic
from the tenant, source key, namespace, and submission ID; row IDs are
deterministic from batch ID and row position, so duplicate source identifiers
remain reportable. Names and email addresses never become correlation keys.
Source record IDs are compared exactly and ordinally; leading and trailing
whitespace is rejected rather than rewritten. The canonical digest includes the
source key, namespace, coverage declaration, row order, and every supplied field
after the documented normalization.

Portia owns an event-sourced `ImportBatch` stream in the tenant realm for staged
content. Fitz projections serve paged preview and progress reads, with their
checkpoint committed with rows. Commands rehydrate authoritative streams for
invariants; they do not decide from a lagged projection. Queries that request a
minimum batch revision report a retryable conflict until the source and
projection reach it. Empty preview pages are never evidence of completion while
the batch is still staging or its projection is behind.

Rows absent from a later submission are never deleted or retired silently. With
`coverage: partial`, absence carries no meaning. With
`coverage: declared_complete`, absence produces proposed retirements under
decision 4. Rows from different source keys are never merged by name; aliases
and duplicate candidates require explicit resolution.

## Tenant, worker, and size boundaries

Every HTTP or MCP request has exactly one `tenant_id` path or argument. Portia
checks current membership, tenant activity, and the required permission before
returning batch metadata, counts, rows, or reports. Missing and cross-tenant
batch IDs have the same not-found result. Reactor messages carry the tenant ID,
source, batch ID, and row ID, and the worker rechecks batch intent, tenant, and
target identity under a named system actor. Fitz rows, checkpoints, and source
correlations use the tenant realm. Standalone and split API/worker hosts share
the same durable progress and retry state.

The first HTTP or MCP staging payload is JSON with at most 200 rows.
`source_key` and `source_namespace` are at most 128 characters,
`source_record_id` at most 256, Application `name` at most 200, and `purpose`
and `owner_reference` at most 2,000 each. The staged event must fit 48 KiB
under Fitz's event frame. There is no file upload, URL fetch, or connector.
A later file path requires EN-06 artifact inspection and a streaming, bounded
worker parser with its own progress and retention.

## Alternatives considered

- **Explicit per-row subset acceptance.** Each accepted row activates after its
  own durable effect, and a later failure leaves the batch
  `partially_accepted`. Rejected: it cannot satisfy "a failed or canceled import
  leaves no partial active records" without changing that acceptance criterion.
- **A transaction across the batch and its target streams.** Not provided by
  Portia or Fitz.
- **Commit marker on the batch stream.** Rejected: it cannot serialize two
  batches from the same source, so claims and declared-complete retirement plans
  could be computed from stale state. The per-source ledger serializes both.
- **Hiding pending records in one list projection only.** Rejected: validators,
  history, and other consumers would still see them.
- **Writing the projection as the authoritative import.** Rejected: it would
  bypass aggregate invariants.
- **Treating a missing source row as an automatic tombstone.** Rejected: it
  would erase governed facts from an unverified observation.

## Consequences

- EN-05 #195 carries the full implementation and the standalone/split-host
  spike, including a crash and restart mid-accept, cancellation before commit,
  and barrier-aware governed reads. Its acceptance criteria are unchanged.
- Every governed consumer of an importable record type must read through the
  barrier. A new consumer that bypasses it breaks decision 2.
- Declared-complete retirement introduces a retired lifecycle state for
  imported record types; each owning story defines what retirement means for
  its records.
- A new import-staging permission lets Contributors stage and preview without
  inventory-management authority.

## Remaining decisions

- [M0-D28 #139](https://github.com/bdgrz/compliance/issues/139) approves the
  canonical source-observation and correlation vocabulary. This ADR uses the
  current [canonical entity model](../../product/canonical-entity-model.md)
  terms and will align with the approved catalog.
- [M0-D05 #62](https://github.com/bdgrz/compliance/issues/62) validates the
  governed Application and SystemInstance boundaries for the first consumer.
- Third-party schemas, connector mappings, and vendor content remain excluded
  unless their exact use is approved under the
  [source-reference policy](../../product/source-reference-policy.md).
- Whole-platform recovery remains owned by
  [cntryl/portia#70](https://github.com/cntryl/portia/issues/70) and DevOps
  under accepted [ADR 0003](0003-event-sourced-history-and-effective-versions.md).

The first-consumer wire contract is
[application-import-v1.md](../../product/application-import-v1.md).

## Public references

No external normative source; original product decision. The relevant internal
model is [domain-model.md](../../product/domain-model.md), and source
eligibility follows
[source-reference-policy.md](../../product/source-reference-policy.md).
