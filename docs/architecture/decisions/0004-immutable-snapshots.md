# Immutable snapshot manifests and amendments

Status: accepted for M0-A02, 2026-09-22. Decision owner: Jeff Repanich (tech
lead). [ADR 0008](0008-snapshot-manifest-regeneration.md) is accepted with it
as the regeneration and amendment-lineage part of this decision. Workforce,
access-population, readiness, and engagement snapshots adopt this mechanism in
their consuming stories; they are not preconditions of this decision.

## Decision

An immutable snapshot records exact tenant-owned source versions, a digest of
each source's canonical content, a canonical manifest, and the manifest digest.
It does not copy mutable current-state projections or infer an audit outcome.
The first consumer freezes one Program plan revision and one approved System
Boundary version as a **program scope record**. This is neither a readiness
assessment nor an examination baseline or management sign-off.

Canonical format v1 serializes each exact source view with the repository's
source-generated snake_case JSON contract, then writes UTF-8 JSON with object
properties sorted by ordinal name, array order retained, strings normalized to
Unicode NFC, integral numbers in decimal, and explicit JSON nulls. UUIDs and
`DateOnly` values use their persisted JSON string representations. A
`DateTimeOffset` retains its persisted offset; this format does not silently
convert it to UTC. The manifest has exactly this property order:
`format_version`, `kind`, `tenant_id`, `program_id`, `program_revision`,
`program_content_sha256`, `boundary_id`, `approved_boundary_version_id`,
`boundary_content_sha256`. SHA-256 hashes the UTF-8 bytes of the corresponding
literal domain prefix (including its trailing zero byte) followed by canonical
JSON bytes. The three prefixes are `bdgrz.snapshot.source.program_revision.v1`,
`bdgrz.snapshot.source.approved_boundary_version.v1`, and
`bdgrz.snapshot.manifest.program_scope.v1`; digests are lowercase hex.

The format version and canonical manifest bytes are retained with the snapshot,
so its stored manifest digest can be verified without reconstructing sources.
The source digests bind the manifest to immutable source revisions. The
read-only verification operation resolves the exact Program revision and
approved Boundary version in tenant-scoped Fitz directories, checks their
canonical v1 digests, and reports `verified`, `missing`, `lag`, or
`digest_mismatch` for each source. It also compares the projected snapshot
against its event-sourced manifest. Overall `verified` is true only when all
three checks pass. A later package regeneration operation must consume this
result and fail on any other status; package regeneration itself is not
implemented by this slice.

An amendment is a new snapshot linked to its immediate predecessor and root,
with an attributable reason. The original and every intermediate manifest stay
immutable. The reason, actor, and linkage are event metadata outside the
content digest. Amendments of unchanged source versions therefore share a
digest. Concurrent amendments of one predecessor can branch; this spike does
not elect a canonical head or latest amendment. Consumers bind an exact
snapshot ID and compare its manifest and ancestry for impact assessment.
Freeze validates all source references before committing its event;
an incomplete attempt creates no visible snapshot. The snapshot event stream
uses the tenant realm. Its Fitz projector commits the query record, list index,
and checkpoint in one transaction. Source commits and projection may be
temporarily separated; a read with a minimum snapshot revision returns a
recoverable conflict while the projection is behind.

## Downstream impact

A consumer (package, engagement baseline, readiness calculation) records the
exact snapshot ID and content digest it used, never "the latest" snapshot.
Amendments are discovered through their `root_snapshot_id` and
`amends_snapshot_id` linkage in the tenant's snapshot directory. Impact is the
set of source references whose exact version or content digest differs between
the bound snapshot and an amendment (`program_revision`,
`approved_boundary_version`), which the pure manifest comparison computes. An
unchanged comparison means the amendment corrected metadata only. The
comparison refuses manifests from different tenants or programs. Deciding what
a consumer does about an impacted source (reopen, re-sign, or note it) belongs
to the consuming story, not to the snapshot mechanism.

## Large populations

Access populations, workforce rosters, and audit populations are not copied
into snapshot events. Each population is an immutable, tenant-owned source
record (for example an accepted access population or a frozen workforce
roster) whose rows have stable keys. A snapshot manifest references that
record by exact identity plus a **population digest**, row count, and chunk
count.

Population digest v1 is streamed and fails closed:

- rows must arrive unique and in ascending ordinal order of their NFC stable
  key; unordered, duplicate, or blank keys, non-integral numbers, and a blank
  population kind are validation failures;
- each row digest is SHA-256 over `bdgrz.snapshot.population.row.v1\0`, the
  UTF-8 key, a zero byte, and the row's canonical v1 JSON;
- rows are grouped into chunks of 1,024; each chunk digest is SHA-256 over
  `bdgrz.snapshot.population.chunk.v1\0` and its row digests in order;
- the population digest is SHA-256 over `bdgrz.snapshot.population.v1\0`,
  the UTF-8 kind, a zero byte, the big-endian 64-bit row count and 32-bit
  chunk count, and the SHA-256 of the ordered chunk digests;
- an explicitly empty population has a defined digest with zero rows, so "no
  rows" is distinguishable from "not captured".

Chunking lets a later verification report the first differing chunk without
re-reading the whole population, and keeps memory constant apart from one
row's canonical bytes.

**Size and performance expectation (technical default).** One population
snapshot supports at most **250,000 rows**; the digest stops reading and fails
at row 250,001 rather than truncating. On a developer machine (Apple M5,
Release build) digesting 250,000 small rows takes about 0.3 seconds, so freeze
and verification cost is dominated by reading the source rows, not by
hashing. This bound is conservative for the first small-team access reviews and
is revised with measured evidence when M0-D07 supplies real population sizes.
Raising it keeps the same fail-closed behavior for older snapshots.

## Options considered

1. **Materialized copies of every source in the snapshot event.** Simple to
   read, but duplicates large populations, makes event streams size-sensitive,
   and conflates the snapshot with package delivery. Rejected.
2. **References to mutable current records.** Cheap, but a snapshot would
   silently change. Rejected.
3. **Exact immutable references plus domain-separated canonical content
   digests (selected).** Reproducible and verifiable without copying, at the
   cost of requiring every source to have durable immutable versions.
4. **One flat digest over a whole population.** Simpler than chunking, but a
   mismatch can only be reported for the whole population and verification
   cannot localize a corrupted range. Rejected in favor of chunked digests.

## Consequences

- A source without durable immutable versions must first acquire them or
  materialize its facts into a separately immutable source record before
  freeze.
- Canonical format and digest domains are versioned; changing either requires
  a new version, and v1 snapshots stay verifiable with v1 rules.
- Consumers must bind exact snapshot IDs; there is no canonical "head" among
  branched amendments.
- The 250,000-row population bound and the eight-link amendment bound are
  explicit technical limits that fail closed rather than degrade.

## Limits

This decision does not set retention periods, legal holds, package
composition or signing, management approval, or measure or prove achievement
of the 15-minute RPO and 4-hour RTO. M0-A01 selects that whole-platform
recovery boundary; [cntryl/portia#70](https://github.com/cntryl/portia/issues/70)
and DevOps own its delivery and timed operational proof. Source completeness
rules and personal approvals belong to the consuming stories: the workforce
snapshot adopts this mechanism in R1-11d, the access population in R2-06a/d,
and engagement baselines in T1-01 and T3-01.

## Thin spike proof

The Program and Boundary streams supply exact versions. Portia authorizes
freeze and amendment as program management operations, and tenant access for
reads. The Fitz directory is tenant-routed. Merged evidence:

- PR #242: freeze and amendment with exact revisions, stable digests after
  later source changes, rejected incomplete freezes, cross-tenant
  non-disclosure, conflict/replay, and projection lag, in standalone and split
  API/worker hosts;
- PR #243: source verification reporting `verified`, `missing`, `lag`, or
  `digest_mismatch` per source over HTTP and read-only MCP;
- PR #321: retained-source recovery and projection replay in both host modes;
- PR #326 ([ADR 0008](0008-snapshot-manifest-regeneration.md)): deterministic
  manifest regeneration from the event-sourced record with a bounded lineage,
  concurrent over HTTP and MCP in split hosting under directory lag;
- the M0-A02 acceptance PR: population digest v1 (stable across property order
  and Unicode normalization, sensitive to row content, kind, and array order,
  fail-closed on ordering and the 250,000-row bound) and manifest impact
  comparison, as focused tests. These are pure, host-independent
  computations, so the standalone and split-host proof above covers the
  persistence and hosting path they plug into.
