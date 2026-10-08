# Directory status reevaluation foundation

This second #277 foundation connects retained operator-owned directory status changes to accepted actual assignments. It adds denial-only receipts and two registered workers. It supplies no partner designation, ratification, public acceptance/removal adapter or professional grant. The compound criteria remain open.

## Source authority and immutable receipts

The global worker reads the exact existing firm-staff directory stream. The internal effect command requires the named Compliance worker, direct invocation and matching reaction causation. Before writing, it hydrates and captures authoritative physical stream positions, rereads the exact directory and original acceptance records, and checks physical offsets, event IDs, full business payload digests, retained original directory decisions and accepted sources. A supplied source payload or system principal alone is insufficient.

Receipt semantic identity uses the directory business request ID plus owning tenant, original acceptance request/engagement and canonical staff/user. It excludes target OCC sequence and current lifecycle revision. Retries return the original committed receipt after legitimate client lifecycle changes; source intent changes are refused. Physical target OCC still serializes every append and a conflict leaves the worker checkpoint pending.

Client-readable history contains a bounded safe directory descriptor (source event/request/physical offset, resulting global source sequence, per-staff revision, canonical identity, active flag and original source fact time), opaque full directory payload digest, and a separately recomputable safe-descriptor digest. Private status reason, staff source reference and status-operator display are not copied into that descriptor. Original and observed accepted snapshots/hashes, named worker attribution and receipt time remain frozen. The full source payload digest binds private evidence at authoritative source verification; local replay cannot cross-read the directory or independently authenticate that opaque digest. Local replay verifies safe-descriptor integrity, semantic identity, retained client facts, exact snapshots/hashes, attribution, chronology and denial-only bounds. A digest is consistency evidence, not a new source authority.

Directory cause time and receipt recording time are separate. Delayed reconciliation can find a status recorded before acceptance committed. Global directory business sequence and per-staff revision are separate: only a cause's staff revision newer than the accepted assignment's captured staff revision applies. Reactivation records another cause and never refreshes old proof. Fresh acceptance capturing revision 3 does not inherit the prior revision-2 cause.

## Complete discovery and lag

The status worker completely enumerates Compliance's permanent registered-tenant catalogue, which includes suspended clients, and every staff/user locator page. Before target discovery, authoritative source coverage uses the SAME client-independence area pattern as the locator projector. Opaque checkpoint equality maps to ordered enumeration position; resource offsets are never compared across resources or parsed as opaque cursor order. A behind/unknown checkpoint cannot stand in for empty history.

Every returned row is checked against authoritative original acceptance. The expected selected-staff locator set must exactly match verified seen rows, including duplicate detection. After paging, accepted event identity/payload sets are read again; changed acceptance sources keep the global cursor pending. Partial target commits are idempotently replayed on retry. Complete discovery is not a global atomic snapshot.

The independent per-tenant accepted-source worker reconciles every retained historical directory status newer than each captured staff revision. It handles delayed indexing and tenant discovery. The source coverage fence additionally closes acceptance reconciled before a later status while the locator remains behind; dual reconciliation alone does not close that race.

## Recovery and scope limits

An affected suspended tenant returns transient pending. A suspended registered tenant with no target assignments needs no receipt effect. Target writer failures, OCC, incomplete discovery, source changes and receipt capacity failure prevent source checkpoint advancement. The complete receipt is prevalidated before append: at most 48 KiB per event and 1,000 retained receipts per client; there is no history truncation or automatic disposal.

Both workloads configure the maximum supported failure-attempt value and bounded retry delay, with durable checkpoint recovery after restart. This is not an independent delivery outbox: an affected tenant that remains inactive, or an unresolved capacity/source problem, can hold later global statuses in FIFO order. Unit-tested recovery does not establish production broker delivery, deployment or professional authority qualification.

A receipt always remains review_required and production_acceptance_blocked. Eligibility denial is specific to the affected accepted engagement/staff/user; unrelated assignees and independent fresh client acceptance preserve existing granular semantics. Receipt history defaults to an immutable empty collection for historical JSON. Public history/OpenAPI changes are additive; internal commands and locators add no public endpoint.

## Evidence boundaries

Tests compose actual AddCompliance registrations, real event store/ledger/directory/tenant mutations, real Fitz locator storage and actual ReactorRunner/checkpoint/request-bus dispatch. Fault wrappers inject missing/duplicate pages, concurrent source publication, writer failure and competing append at owning boundaries. Internal accepted proof and ratified rules are explicitly synthetic fixtures and establish no real professional decision. The history-exhaustion test uses explicitly synthetic safe descriptors to fill the bounded aggregate, then proves an actual registered worker leaves a new retained directory cause pending and an exact committed semantic retry remains allowed at capacity; those synthetic descriptors establish no external directory authority.

The 201-client case proves complete registered-tenant traversal and effects. The accepted locator's separate 210-row case proves general row paging (30 staff across seven engagements). Each authoritative client ledger permits at most 100 engagements and unique selected staff per acceptance, so no fabricated greater-than-200 per-staff prefix is used as proof. Broader partner/current authority and actual ratified-rule change sources remain separate owning gates.
