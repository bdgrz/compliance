# Artifact content storage and inspection boundary

Status: accepted boundary decision for M0-A03, 2026-09-21. It closes #83 by
rejecting a single-value Fitz KV implementation for the 256 KiB content target.
It does not choose a production blob provider or make EN-06 or its backend
child [#196](https://github.com/bdgrz/compliance/issues/196) ready to close.

## Proposed content contract

The first artifact-content adapter is intended to keep content identity within
one tenant. Its proposed public contract is:

```csharp
public sealed record ArtifactContentReference(Uuid TenantId, string Sha256, long Length);
public sealed record ArtifactContentWrite(ArtifactContentReference Content, bool AlreadyPresent);

public interface IArtifactContentStore
{
    ValueTask<ArtifactContentWrite> StoreIfAbsentAsync(
        Uuid tenantId, ReadOnlyMemory<byte> content, CancellationToken ct = default);
    ValueTask<ReadOnlyMemory<byte>?> ReadVerifiedAsync(
        ArtifactContentReference content, CancellationToken ct = default);
}

public interface IArtifactInspector
{
    ValueTask<ArtifactInspectionResult> InspectAsync(
        ArtifactContentReference content, CancellationToken ct = default);
}
```

`sha256` would be the lowercase hexadecimal SHA-256 digest of the exact content
bytes. The intended Fitz location is one tenant-owned route per organization,
`kv://{tenant_id}/artifact_content/v1`, using `sha256:{lower_hex_digest}` as
the immutable identity key. An identical digest in a different tenant must use
a different route and must not be deduplicated across those routes. A future
store must write with synchronous durability and return `AlreadyPresent` only
after verifying the stored bytes against the requested reference.

The proposed input cap is 256 KiB. It bounds the first consumer before the
product decides larger-file handling or resumable upload semantics. It is a
target contract, not a claim that the current application can accept 256 KiB
artifact content.

No artifact HTTP operation, MCP tool, Portia aggregate, projector, reactor, or
DI registration is introduced by this spike. In particular, it does not grant
artifact access from a related record, make a download decision, record a
delivery, classify content, quarantine a file, or claim a malware inspection.

## Decision and Fitz evidence

Fitz encodes each KV operation in a wire payload whose length is a 16-bit
value. A KV insert carries its transaction identifier, route, key, and value
in that one payload, leaving less than the protocol maximum for the value
itself. The 256 KiB target therefore cannot be represented as one Fitz KV
value.

EN-06 must not implement its 256 KiB artifact-content requirement as a
single-value Fitz KV record. It must not conceal that limit by adding a
manifest-and-chunk protocol without a separately reviewed design and recovery
proof.

`ArtifactContentStorageProbeE2ETests.ShouldRejectTwoHundredFiftySixKiBContentGivenFitzWireLimit`
uses the real Fitz broker configured for this repository. It opens the intended
tenant route with synchronous read-write durability and confirms that a 256 KiB
value fails locally with `ProtocolException` before it can become committed.
This is a protocol boundary, not a transient broker failure.

Writing content chunks and a manifest could avoid the individual frame limit,
but it would add a multi-key visibility, retry, corruption-detection, and
recovery protocol. That protocol needs an explicit design and proof before it
can be treated as immutable artifact storage. It is intentionally not hidden
inside this adapter. Selecting an external blob service also needs separate
operational decisions for encryption, key ownership, delivery authorization,
and recovery evidence.

## Consequences and follow-up

The 256 KiB content-store contract remains blocked. #196 stays open until the
team chooses and proves a storage mechanism that can meet the cap and the
immutable, tenant-isolated write/read semantics above. Its future implementation
must then add the content-store interfaces and a real broker or selected-store
test that proves both the supported bound and cross-tenant isolation.

M0-D16 still owns retention, holds, disposition, and backup expectations.
EN-06 still owns upload validation, inspection, quarantine, artifact-level
authorization, derived-artifact lineage, and delivery logging. This spike does
not select a physical encryption mechanism, an inspection engine, a quarantine
workflow, presigned URLs, export behavior, or a deletion policy.

## Alternatives considered

- A single Fitz KV value preserves a small, simple atomic write-once shape, but
  cannot satisfy the proposed 256 KiB input cap because of the wire limit.
- A manifest plus content chunks could fit individual Fitz frames, but is a new
  storage protocol with failure semantics that this ADR has not approved.
- An object/blob service can support larger content, but its tenant partition,
  encryption, authorization, and recovery properties need separate evidence.

## Evidence

The focused broker test is intentionally direct: it exercises the same
Fitz `IKvClient` that a future adapter would receive from Portia's shared
connection, without adding a second client or a test-only transport. The
repository's [event-sourcing decision](0003-event-sourced-history-and-effective-versions.md)
continues to apply to artifact metadata once a content mechanism is chosen;
it does not make Fitz KV suitable for the blocked 256 KiB value.
