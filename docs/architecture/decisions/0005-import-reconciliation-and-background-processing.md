# Import reconciliation and background processing

Status: proposed for M0-A06 review. This is a technical design and contract
draft, not an accepted ADR or a completed thin spike. Decision owner: tech lead
and product owner. Date: 2026-09-20.

## Proposed common constraints for the first consumer

The first consumer accepts bounded, tenant-supplied Application rows as an
original product input. An import is an observation from a named source, not
an authoritative Application, verified owner, classification, or scope
decision. The tenant chooses a stable `source_key` and `source_namespace`,
then supplies a `source_record_id` per row. The source tuple is tenant ID,
source key, namespace, object kind (`application`), and exact source record ID.
These are claims by that tenant; they do not imply connector authentication
or rights to third-party source material. The server
records the submitter, submission time, source tuple, coverage declaration,
ordered raw field values, and a server-computed SHA-256 of canonical input.
Nothing reaches the governed Application inventory during staging or preview.

A caller supplies a UUID `submission_id` for one intended observation. Within
one tenant, source key, and namespace, repeating that ID with identical
canonical input
returns the same batch and state; changing the input returns a conflict. A new
submission ID may repeat the same content and remains a new observation whose
rows can be reported unchanged. A batch ID is deterministic from the tenant,
source key, namespace, and submission ID; row IDs are deterministic from batch ID and row
position so duplicate source identifiers remain reportable. Names and email
addresses never become correlation keys. Source record IDs are compared exactly
and ordinally; leading/trailing whitespace is rejected rather than rewritten.
The canonical digest includes the source key, namespace, coverage declaration,
row order, and every supplied field after
the documented normalization; it excludes transport whitespace and request
metadata. Changing any of those values under one submission ID conflicts.

Portia owns an event-sourced `ImportBatch` stream in the tenant realm. It
records staged rows, validation findings, explicit row decisions, application
intent and outcome, cancellation, and retries. Fitz projections serve paged
preview and progress reads, with their checkpoint committed with rows.
Commands rehydrate the batch and the target Application stream for invariants;
they do not decide from a lagged projection. Queries that request a minimum
batch revision report a retryable conflict until the source and projection
reach it. Empty preview pages are never evidence of completion while the
batch is still staging or its projection is behind. The preview also checks
the tenant Application-directory checkpoint against its event source before
claiming that no existing Application matches a source claim.

## Unresolved product acceptance: failure and cancellation

The [EN-05 backlog acceptance](../../product/backlog.md#en-05-import-with-preview-reconciliation-and-safe-replay)
and [backend child #195](https://github.com/bdgrz/compliance/issues/195)
require a failed or canceled import to leave no partial active records. The
same backlog permits atomic **or explicit-subset acceptance**. Those words do
not resolve what happens when one accepted row has already become active and
a later row fails or the batch is canceled. Portia's one-stream commits and
reactor effects cannot make all Application streams atomic. This is a product
acceptance decision, not a technical default delegated by this ADR.

Two viable directions need product-owner review:

1. **Strict no-partial visibility:** keep every imported Application effect
   invisible to all governed reads and validators until a durable batch
   visibility barrier commits. Failure/cancellation before that barrier must
   leave no active record. This needs a proven visibility and recovery protocol
   across all consumers; writing rows and hiding them only in one list would
   not satisfy the existing acceptance.
2. **Explicit per-row subset:** each accepted row becomes active after its own
   durable effect. The authoritative batch aggregate rejects cancellation
   once any row intent is accepted, including one whose effect is pending.
   Terminal `failed` and `canceled` are possible only with zero accepted
   intents. A later row failure leaves the batch `partially_accepted` with
   row-level retry or resolution state. Whether that state meets the backlog
   phrase "a failed import" needs an explicit product interpretation or
   acceptance amendment. It is the candidate contract below, **not an
   approved decision**.

The acceptance and cancellation commands, their terminal states, and any
claim of EN-05 backend completion are blocked until the product owner selects
and records a path. Staging, preview, source identity, tenant isolation, and
replay design can be reviewed independently.

## Candidate B: acceptance and failure semantics

Acceptance is an explicit subset of **one row per decision**. A decision says
`create`, `link_existing`, or `skip`, identifies the target when needed, and
requires the exact batch revision and, for an existing Application, its
expected revision. Duplicate identifiers, ambiguous matches, invalid rows,
and unresolved links cannot be applied. `skip` records an attributed decision
without reserving or binding a source claim; a later submission may resolve
that source ID. `link_existing` adds a source
observation; it does not overwrite governed name, purpose, owner, or scope.
`create` makes a new Application with its imported provenance and unresolved
owner/classification where appropriate. No batch-wide atomicity is claimed:
Portia commits one aggregate stream at a time, and a reactor cannot make the
batch and Application streams one transaction.

For `create` or `link_existing`, the row decision first records a durable intent
with a deterministic effect ID
derived from the batch, row, and immutable decision sequence, rather than Portia's
reaction request ID, which can change on replay. A source-claim binding keyed
by the exact source tuple serializes correlation: its first accepted decision
fixes one governed Application ID, and a competing binding conflicts rather
than creating a duplicate across batches. The claim stream first records a
pending reservation tied to the accepted intent and target. A retry with that
same intent resumes it; a different target conflicts. The internal Application
command checks its effect ID before its expected revision, so a committed
effect whose batch outcome was lost can be recognized on replay. A transient
or unknown failure keeps the intent pending and cannot be re-decided. A
permanent stale-target conflict records a terminal `needs_resolution` outcome
only after rehydrating the authoritative target Application stream, finding
no event with that effect ID, and proving its current revision is strictly
greater than the old expected revision. Because Application revisions are
monotonic, the old expected revision cannot later succeed. A timeout, stale
projection, or absent batch outcome is never this proof; crash ambiguity
remains pending until authoritative stream readback resolves it.
An authorized human may then supersede that exact decision with a new
expected batch revision, new target/expected Application revision or `skip`,
and a rationale. The old decision and outcome remain immutable. The source
claim stream transitions its reservation to the new decision only after the
terminal nonapplied proof. Supersession by `skip` records a reviewed
release/unbound transition after that proof while retaining the earlier
reservation in history. The reactor checks the current reservation before
each effect. A competing target otherwise conflicts. A failure without a
terminal nonapplied proof never releases a claim. The claim,
Application, and batch commits are separate; neither the reservation nor a
later `bound` state makes them atomic. A tenant-scoped reactor
applies the intent through a distinct internal import-effect command and
authorizer. That authorizer checks the persisted accepted batch row, tenant
realm and target, rather than giving `RequestActor.System` blanket access to
the existing user-only Application inventory commands. The effect preserves
the original importing member's attribution as well as the named system actor.
Its target Application ID is stable across retry and re-import. It then records
the row outcome. A crash after the Application commit but before the outcome
is retried against the same identity and completed without a second
Application. A conflict with a changed target is visible and requires the
explicit superseding decision above; it is never silently rematched. A
`create` or `link_existing` row is visible
only after its own Application command commits. The batch aggregate permits
cancellation only before its first durable accepted row intent, so a canceled
batch has no active imported Application. A cancellation after any accepted
intent returns 409, even if the effect has not committed. A terminal `failed`
batch likewise has zero accepted intents. If a later row effect fails after
an earlier row commits, the batch remains `partially_accepted` with the
failed row visible for retry or resolution; no atomic rollback is claimed.
The interpretation of this partial state remains the product decision above.

A row absent from a later submission is never deleted or retired. With
`coverage: partial`, absence carries no missing-row meaning. With
`coverage: declared_complete`, comparison against prior accepted observations
from the same source key and namespace emits `missing_from_source` candidates, including the
declaring actor and time. It remains an unverified source claim and cannot
change governed scope, status, or inventory without a separate authorized
decision. Rows from different source keys are never merged by name; aliases
and duplicate candidates require explicit resolution.

## Tenant, worker, and size boundaries

Every HTTP or MCP request has exactly one `tenant_id` path/argument. Portia
checks current membership, tenant activity, and the existing Application
inventory permission before returning batch metadata, counts, rows, or
reports. Missing and cross-tenant batch IDs have the same not-found result.
The actor snapshot for staging and each decision is retained. Reactor
messages carry the tenant ID, batch ID, row ID, and causal request ID; the
worker rechecks batch intent, tenant, and target identity and uses a named
system actor. A system actor alone is not authority to accept a new row.
Fitz rows, checkpoints, and source correlations use the tenant realm. Standalone
and split API/worker hosts share the same durable progress and retry state.

The first HTTP or MCP staging payload is JSON with at most 200 rows. `source_key` is at most
128 characters, `source_record_id` at most 256, Application `name` at most
200, and `purpose` and `owner_reference` at most 2,000 each. `source_namespace`
is at most 128 characters. The existing
Portia HTTP JSON-body limit is 10 MiB; the row and field limits bound this
first consumer below it. Validation rejects extra rows or overlong values;
Portia returns 413 for an oversized body. There is no file upload, URL fetch,
connector, or claim to support large files in this slice. A later file path
requires EN-06 artifact inspection and a streaming, bounded worker parser
with its own progress and retention rules. The batch records limits and
failure states without pretending that path has been delivered.

## Alternatives and remaining decisions

A transaction across an import batch and many Application streams would give
all-or-nothing activation but is not provided by Portia/Fitz. A projection
write as the authoritative import would bypass aggregate invariants. Matching
by name would merge unrelated systems. Treating a missing source row as a
tombstone would erase governed facts from an unverified observation. The
candidate accepted-subset design exposes each consequence and its recovery
state but awaits the product interpretation above.

M0-D28 must approve canonical source-observation and correlation vocabulary;
M0-D05 must validate the governed Application and SystemInstance boundaries;
M0-D03 must settle any new import-specific grant and restricted-discovery
policy. Until then the first consumer uses the existing `program.manage`
inventory authorization and does not assert a verified owner, classification,
or source authority. Third-party schemas, connector mappings, and vendor
content remain excluded unless their exact use is approved under the public
source-reference policy. Product owners must confirm whether a declared
complete list is meaningful for any specific source, and how long raw rows
and rejected reports are retained. M0-A01 recovery targets and an isolated
restore exercise remain open. None of these open points is silently answered
by this proposal.

The upstream mapping work in
[cntryl/portia#60](https://github.com/cntryl/portia/issues/60) is closed for a
stale append after session acquisition. The current real-broker Compliance
two-writer test is still red because the second program save is rejected first
at append-session admission with `Cntryl.Fitz.StreamException` domain code
`2002`, `StreamSessionAlreadyActive`. The Portia adapter does not yet translate
that structured, transient stream-write contention into its public conflict
surface, so HTTP returns 500 and MCP returns an internal failure. The same
evidence occurs in standalone and split API/worker hosts. This draft does not
prescribe an application catch or automatic command retry. The implementation
must not claim a complete concurrency contract until the adapter behavior is
corrected and proved with the real broker. The adapter work is tracked in
[cntryl/portia#65](https://github.com/cntryl/portia/issues/65).

The first-consumer wire contract is
[application-import-v1.md](../../product/application-import-v1.md). A thin
standalone/split spike, failed-intent replay, and exact source-to-projection
proof are required before this ADR is accepted or #86 is closed.

## Public references

No external normative source; original product decision. The relevant
internal model is [domain-model.md](../../product/domain-model.md), and source
eligibility follows [source-reference-policy.md](../../product/source-reference-policy.md).
