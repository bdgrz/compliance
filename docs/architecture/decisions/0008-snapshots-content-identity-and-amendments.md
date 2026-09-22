# Snapshot content identity and amendment regeneration

Status: proposed technical default for M0-A02, 2026-09-22. This record
extends [the immutable-snapshot decision](0004-immutable-snapshots.md) with a
narrow program-scope manifest-regeneration operation. It does not accept the
remaining M0-A02 product or operational decisions. Issue
[#82](https://github.com/bdgrz/compliance/issues/82) remains open.

Decision owner: tech lead and product owner.

## Decision

A frozen program-scope snapshot retains its `ProgramScopeManifest`, canonical
v1 manifest JSON, and lowercase SHA-256 content identity in its authoritative
`SnapshotFrozen` event stream. The `ImmutableSnapshot` aggregate is the source
for those retained facts. The snapshot directory is a query projection and is
not authority for regeneration.

`manifest_regeneration` is a tenant-scoped, read-only operation for a requested
program-scope snapshot ID. After tenant access is authorized, its handler
hydrates that snapshot aggregate and checks that it is frozen, belongs to the
request tenant, has the program-scope kind and consistent program and amendment
linkage, and that the retained canonical JSON and digest match the retained v1
manifest. It then recomputes canonical v1 JSON and the SHA-256 digest from the
retained manifest and returns those recomputed values. It does not echo a
stored string without checking it, read a snapshot-directory row, or resolve
the current Program or Boundary state.

The v1 canonical format, domain-separated content digests, explicit format
version, and source references remain those defined in decision 0004. A future
format changes its format version and content-identity domain. It must retain
the v1 bytes and identity as historical facts; it cannot reinterpret a v1
snapshot under a new serializer or format.

An amendment is a distinct immutable snapshot with its own ID, content
identity, immediate predecessor, root snapshot, reason, and actor. Regeneration
uses the requested snapshot ID and never follows an amendment chain to a newer
record. It does not overwrite, normalize, or update the original or an
intermediate snapshot. A later Program or Boundary change therefore has no
effect on an existing snapshot's regenerated v1 manifest.

The operation is manifest regeneration, not package regeneration. It does not
verify live source availability, make an examination package, produce a
signature, establish management approval, or determine an audit outcome.
Those actions need their own domain terms, authorization, retention, and
evidence.

## Authorization and projection boundary

"Retained-source" describes the data path after authorization. Portia first
uses the normal tenant access and policy projections to authorize the caller;
only then may the handler read the event-sourced snapshot aggregate. The
authorization outcome follows the existing tenant-access policy and must not
include the snapshot's manifest, content identity, existence, or lineage.

This design lets an authorized regeneration read continue while the snapshot
directory has not caught up. It does not make regeneration available after the
authorization projections are absent, stale, or unrecoverable. Split-host proof
must establish access, program, and boundary authorization state before
stopping a worker; the resulting test demonstrates that a snapshot-directory
lag does not substitute for source authority, not that authorization can be
bypassed.

The HTTP operation and its MCP tool use the same Portia request contract. Their
route values and JSON properties are snake_case, and the MCP tool is marked
read-only. Human approval, attestation, acknowledgment, invitation acceptance,
and other personal sign-offs remain HTTP-only.

## Evidence required

The thin M0-A02 implementation needs evidence that:

- aggregate hydration recomputes the retained v1 identity and rejects a
  missing, non-program-scope, or internally inconsistent frozen source with a
  permanent domain error;
- the authorized HTTP operation, read-only MCP tool, and OpenAPI contract
  expose the same tenant-scoped result and do not disclose it to an outsider or
  another tenant;
- standalone and split API/worker hosts can return regenerated source facts;
  in split mode, after the authorization preconditions have caught up, a stopped
  worker leaves an ordinary snapshot-directory read lagged while regeneration
  continues, and the ordinary read recovers when the supported worker restarts;
- retained-event replay and broker restart preserve the same manifest identity,
  while concurrent attempts cannot rewrite an already frozen snapshot; and
- focused tests, the applicable broker suite, formatting, and the hosted
  Native AOT container checks pass before the delivery is merged.

## Alternatives and limits

Returning a directory row would couple reproducibility to projector catch-up
and could return a stale or corrupted derived record. Reconstructing a manifest
from current Program and Boundary records would make an immutable snapshot
silently change after its sources change. Materializing every future package in
the snapshot event would conflate the package's own authorization, signing,
delivery, and retention obligations with this small source record.

This decision selects only the program-scope manifest path. The remaining
snapshot types, workforce population and source-completeness rules, measured
size and regeneration-throughput limits, package composition, signing,
management sign-off, retention, holds, deletion, portable backup and restore,
and recovery targets remain unresolved. M0-A01 still owns the accepted
operational recovery prerequisites. #82 cannot close until its ADR is accepted
and the dependent product and operational evidence is complete.
