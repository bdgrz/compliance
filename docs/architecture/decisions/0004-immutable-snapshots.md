# Immutable snapshot manifests and amendments

Status: proposed technical default and thin spike for M0-A02, 2026-09-20.
M0-A02, workforce, and engagement snapshot acceptance remain open.

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

## Alternatives and limits

Copying every source payload into every snapshot would duplicate large
populations and make event streams size-sensitive. Referring to mutable current
records without exact versions or content digests would lose reproducibility.
The selected manifest uses exact immutable references with content checks. A
future source without durable immutable versions must first acquire them or
materialize its facts into a separately immutable source record before freeze.

This spike does not set workforce population limits, retention periods, legal
holds, package regeneration throughput, or measure or prove achievement of the
15-minute RPO and 4-hour RTO. M0-A01 selects that whole-platform recovery
boundary; [cntryl/portia#70](https://github.com/cntryl/portia/issues/70) and
DevOps own its delivery and timed operational proof. Workforce snapshots, source
completeness rules, and personal approvals belong to their consuming stories.

## Thin spike proof

The Program and Boundary streams supply exact versions. Portia authorizes
freeze and amendment as program management operations, and tenant access for
reads. The Fitz directory is tenant-routed. Focused and split API/worker tests
should cover stable digests after later source changes, rejected incomplete
freezes, cross-tenant non-disclosure, conflict/replay, and projection lag and
recovery. M0-A02 remains open until its
workforce consumer and measured size/performance expectations are accepted.
