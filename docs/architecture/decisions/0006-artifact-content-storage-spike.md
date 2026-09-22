# Artifact content storage, inspection, and access

Status: accepted for M0-A03, 2026-09-22. Decision owner: Jeff Repanich (tech
lead). The production adapter is delivered by Portia
([cntryl/portia#69](https://github.com/cntryl/portia/issues/69)). The real-S3
proof is deferred to [EN-06 backend #196](https://github.com/bdgrz/compliance/issues/196).
This ADR does not make EN-06 or #196 ready to close.

## Context

Evidence, policy files, provider reports, and packages need immutable content
identity, safe handling, and per-artifact authorization. The earlier spike
showed that one Fitz KV value cannot hold artifact content (see
[Rejected options](#rejected-options)). Decision inputs were recorded on
[#83](https://github.com/bdgrz/compliance/issues/83) on 2026-09-22. This
record accepts them as one design.

## Decision

### Ownership boundary

Compliance application and domain code depend only on a vendor-neutral,
capability-oriented port, `IArtifactContentStore`. It has no AWS SDK types,
bucket names, KMS identifiers, multipart details, or pre-signed URL shapes.
Reusable object storage, encryption-key selection, multipart transfer, S3
signing, and vendor adapters belong in Portia. Compliance keeps artifact
lifecycle, provenance, record-level authorization, inspection and quarantine
decisions, retention, hold, and disposition, and domain events. This
repository must not add an AWS SDK dependency or a bespoke S3 adapter.

```csharp
public sealed record ArtifactContentReference(Uuid TenantId, string Sha256, long Length);
public sealed record ArtifactContentWrite(ArtifactContentReference Content, bool AlreadyPresent);
public sealed record ArtifactDelivery(ArtifactContentReference Content, Uri Location, DateTimeOffset ExpiresAt);

public interface IArtifactContentStore
{
    ValueTask<ArtifactContentWrite> StoreIfAbsentAsync(Uuid tenantId, Stream content, CancellationToken ct = default);
    ValueTask<Stream?> OpenVerifiedAsync(ArtifactContentReference content, CancellationToken ct = default);
    ValueTask<ArtifactDelivery?> IssueDeliveryAsync(ArtifactContentReference content, TimeSpan lifetime,
        CancellationToken ct = default);
    ValueTask<bool> DeleteAsync(ArtifactContentReference content, CancellationToken ct = default);
}

public interface IArtifactInspector
{
    ValueTask<ArtifactInspectionResult> InspectAsync(ArtifactContentReference content, CancellationToken ct = default);
}
```

When Portia #69 ships, the S3 adapter implements this port by delegating to
Portia's object-transfer contracts. Resumable upload sessions (create, sign
part, complete, abort) are added to the port at that time, in the same
vendor-neutral shape. The port does not mirror the SDK.

### Store and layout

- **Provider:** Amazon S3 through the Portia adapter is the only production
  store.
- **Partitioning:** one dedicated bucket per tenant. The tenant resolves to its
  bucket and encryption-key reference through Portia interfaces, never from
  request input.
- **Content identity:** the full lowercase SHA-256 digest of the exact bytes,
  together with the byte length. Objects live at
  `content/sha256/{first_two_hex}/{sha256}` inside the tenant bucket.
  `quarantine/` and `derived/` are reserved prefixes with no transitions
  decided here.
- **Immutability:** writes are conditional creates. An existing digest is never
  replaced. The store reports `AlreadyPresent` only after the stored bytes
  verify against the requested digest and length. A mismatch is a corruption
  failure, not a success.
- **No cross-tenant deduplication:** identical bytes in two tenants are two
  objects in two buckets. A reference is only valid inside its own tenant.

### Encryption

S3 SSE-KMS with one AWS-managed KMS key initially. The application models a
per-tenant encryption-key reference now, so later per-tenant physical keys need
no contract change. Every encryption operation passes through Portia's common
encryption interface. Key rotation, recovery, and key-administrator authority
are operational controls owned by Portia #69 and DevOps.

### Upload

- The maximum artifact size is 256 MiB (`ArtifactContentLimits.MaxContentLength`).
  The store rejects larger content before it becomes visible.
- Uploads use resumable S3 multipart upload through Portia. Explicit
  cancellation deletes incomplete upload state.
- **Technical defaults, owned by Portia #69:** a bucket lifecycle rule aborts
  incomplete multipart uploads after 24 hours. Every part and the completed
  object carry a SHA-256 checksum. Completion states the expected length and
  digest and fails on mismatch.

### Inspection and quarantine

`IArtifactInspector` is the storage-level hook. The registered default,
`UninspectedArtifactInspector`, always reports `NotInspected`. The product
never shows content as clean until an inspection engine is selected. The
engine, the quarantine user workflow, and the release rules are product and
security decisions assigned to M0-D16 and EN-06. They are not decided here.

### Download authorization and delivery

- Delivery uses short-lived S3 pre-signed URLs, not an application-proxied
  download.
- The application issues a delivery only after the Portia request authorizer
  for the owning artifact record allows the actor. A denial reuses that
  record's not-found or forbidden result, so a reference alone never grants
  access. The store port does not make authorization decisions.
- **Technical default:** the delivery lifetime is at most 5 minutes
  (`ArtifactContentLimits.MaxDeliveryLifetime`). The store rejects longer or
  non-positive lifetimes.
- **Revocation:** an issued pre-signed URL cannot be revoked before it expires.
  Revoking access therefore stops future issuance immediately, and exposure is
  bounded by the lifetime cap. This is why the cap is short.
- **Delivery logging:** every issuance records a durable, tenant-scoped domain
  event (actor, artifact, content reference, purpose, and expiry) before the
  location is returned. Object-level access logging on the bucket is
  infrastructure evidence owned by Portia #69 and DevOps. The event contract
  lands with EN-06's first artifact aggregate.

### Retention, hold, and disposition hooks

Artifacts have no time-based storage expiry. Content is kept until an
authorized deletion. `DeleteAsync` is the only storage-level disposition hook,
and the application may call it only after its retention and hold rules
permit deletion. The deletion authority, legal-hold precedence, disposition
audit record, and per-organization export are policy owned by
[M0-D16 #73](https://github.com/bdgrz/compliance/issues/73) and delivered by
[R2-12](https://github.com/bdgrz/compliance/issues/285). Offboarding export and
bucket disposition follow the tenant lifecycle in
[F1-01](https://github.com/bdgrz/compliance/issues/286).

## Spike evidence

`LocalArtifactContentStore` is the development and spike adapter. It maps each
tenant bucket to a tenant directory with the same key layout. It stages content
while hashing, then publishes it with a no-overwrite move. It verifies digest
and length on every open. Its HMAC-signed, expiring delivery locations stand in
for pre-signed URLs. It fails closed when `Artifacts:LocalContentRoot` or
`Artifacts:LocalDeliveryKey` is not configured. It is not a production store.

- `LocalArtifactContentStoreTests` (20 cases) prove conditional create and
  verified `AlreadyPresent`, no cross-tenant deduplication, digest and length
  verification on read, rejection of non-canonical or path-traversal digests,
  the content-length limit with no leftover content, bounded, unforgeable, and
  expiring delivery, tenant-scoped disposition, fail-closed configuration, and
  the never-clean default inspector.
- `ArtifactContentHostModeE2ETests` resolve the port from the shared composition
  in a standalone host. They also cover a split `api` host plus a worker host on
  the real broker. Content stored by the API host is verified and delivered by
  the worker host. A cross-tenant reference returns nothing, and identical
  content in a second tenant is stored separately.

## Deferred proof

[EN-06 backend #196](https://github.com/bdgrz/compliance/issues/196) is blocked
by [cntryl/portia#69](https://github.com/cntryl/portia/issues/69). It must
prove, against the Portia S3 adapter and an S3-compatible store in standalone
and split hosts, the same behavior this spike proves locally. It must also
prove multipart resume and abort, SSE-KMS key resolution, pre-signed delivery
expiry, and the delivery-issued event.

## Rejected options

- **A single Fitz KV value.** Fitz encodes each KV operation in a payload with
  a 16-bit length, so a 256 KiB value cannot be represented.
  `ArtifactContentStorageProbeE2ETests.ShouldRejectTwoHundredFiftySixKiBContentGivenFitzWireLimit`
  proves this against the real broker.
- **A manifest plus content chunks in Fitz KV.** This is a new storage protocol
  with multi-key visibility, retry, corruption, and recovery semantics that
  would need its own design and proof. An object store already provides them.
- **An application-proxied download.** It puts large transfers on API hosts and
  duplicates what pre-signed delivery provides. It was rejected in favor of
  authorized issuance plus short-lived URLs.
- **A shared bucket with tenant prefixes.** It is simpler to provision, but
  isolation then depends on key construction alone. Per-tenant buckets give
  per-tenant policy, key selection, export, and disposition.

## Consequences

- Artifact metadata stays event-sourced under
  [ADR 0003](0003-event-sourced-history-and-effective-versions.md). Only
  content bytes live in the object store.
- Recovery of object content is part of the whole-platform recovery capability
  ([cntryl/fitz#259](https://github.com/cntryl/fitz/issues/259)) and DevOps
  evidence. Bucket versioning or replication settings are chosen there.
- Features that need content wait for #196, not for this ADR.
