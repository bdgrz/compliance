# Snapshot manifest regeneration and bounded amendments

Status: accepted with [ADR 0004](0004-immutable-snapshots.md) for M0-A02
([#82](https://github.com/bdgrz/compliance/issues/82)), 2026-09-22. This record
extends the immutable-snapshot decision with a narrow program-scope
manifest-regeneration operation.

Decision owner: Jeff Repanich (tech lead).

## Decision

A frozen program-scope snapshot retains its `ProgramScopeManifest`, canonical
v1 manifest JSON, and lowercase SHA-256 content identity in the authoritative
`SnapshotFrozen` event stream. The `ImmutableSnapshot` aggregate is the source
for these retained facts. The snapshot directory is a query projection and is
not authority for regeneration.

`manifest-regeneration` is a tenant-scoped, read-only operation for a requested
program-scope snapshot ID. After tenant access is authorized, its handler
hydrates that snapshot aggregate, verifies its retained v1 manifest and
amendment lineage, then recomputes the canonical v1 JSON and SHA-256 digest.
It does not return an unchecked stored string, read a snapshot-directory row,
or resolve mutable current Program or Boundary state.

A root has amendment depth zero. An amendment has its predecessor's depth plus
one. A predecessor chain may contain at most **eight** amendment links, so one
regeneration may hydrate at most nine snapshot aggregates including the leaf.
Branches remain allowed because the limit applies to each exact predecessor
chain, not all amendments of a root. Creation verifies the predecessor against
a budget of seven links before appending, while regeneration verifies the
requested leaf against the eight-link budget. Over-limit or malformed retained
lineages fail closed with a non-transient conflict; the system neither flattens,
rewrites, nor skips historical snapshots.

The limit is a conservative technical default that prevents an authorized
caller from turning historical correction depth into an unbounded HTTP or MCP
read. A later increase requires measured operating requirements and replacement
evidence; it must retain the same fail-closed behavior for older data.

The HTTP operation is:

`GET /api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/manifest-regeneration`

Route segments are hyphenated while interpolated route values remain
`snake_case`. The same request contract exposes the read-only MCP tool
`bdgrz.snapshot.program_scope.manifest_regenerate`. Human approval,
attestation, acknowledgement, invitation acceptance, and other personal
sign-offs remain HTTP-only.

## Authorization and projection boundary

Retained-source describes the data path after authorization. Portia first
applies normal tenant access and policy projections; only then may the handler
read the event-sourced snapshot aggregate. An authorization failure must not
disclose the snapshot's manifest, content identity, existence, or lineage.

The source operation may succeed while the snapshot directory is lagged. This
does not make it available when authorization projections are absent, stale, or
unrecoverable. In split hosting, authorization state must be established before
the worker is stopped; source regeneration then proves that snapshot-directory
lag does not substitute for source authority.

## Evidence required

The thin M0-A02 implementation must prove that:

- retained aggregate hydration recomputes the v1 identity and rejects missing,
  malformed, cyclic, cross-tenant, or over-limit source lineage;
- a leaf at eight links succeeds, a ninth amendment is rejected before append,
  and an over-limit retained leaf stops before a tenth snapshot hydration;
- authorized HTTP and read-only MCP contracts return the same retained result;
  outsiders and another tenant receive no snapshot disclosure;
- standalone and split API/worker hosts support regeneration; in split mode the
  operation survives snapshot-directory lag and succeeds concurrently over HTTP
  and MCP at the supported bound; and
- retained replay preserves the original manifest identity after a later
  amendment, with focused tests, broker coverage, formatting, and Native AOT
  checks passing before merge.

## Alternatives and limits

Returning a directory row would couple reproducibility to projector catch-up
and could return stale or corrupted derived content. Reconstructing a manifest
from mutable current records would make a snapshot silently change. Materializing
every future package in a snapshot event would conflate package authorization,
signing, delivery, and retention with this source record.

This decision selects only program-scope manifest regeneration. Population
size and digest rules are in ADR 0004. It does not set package composition, signing, management approval,
retention, holds, deletion, portable backup and restore, general request
admission/rate limits, or recovery targets. M0-A01 accepts the application
recovery boundary; whole-platform recovery delivery and timed operational
evidence are tracked in [cntryl/portia#70](https://github.com/cntryl/portia/issues/70)
with DevOps.
