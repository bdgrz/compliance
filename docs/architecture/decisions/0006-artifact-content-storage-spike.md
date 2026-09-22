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

    // Hold, quarantine, and disposition hooks. Policy lives in product records.
    ValueTask<bool> PlaceHoldAsync(ArtifactContentReference content, Uuid holdId, CancellationToken ct = default);
    ValueTask<bool> RemoveHoldAsync(ArtifactContentReference content, Uuid holdId, CancellationToken ct = default);
    ValueTask<bool> QuarantineAsync(ArtifactContentReference content, CancellationToken ct = default);
    ValueTask<bool> ReleaseQuarantineAsync(ArtifactContentReference content, CancellationToken ct = default);
    ValueTask<Stream?> OpenQuarantinedAsync(ArtifactContentReference content, CancellationToken ct = default);
    ValueTask<ArtifactDeletionResult> DeleteAsync(ArtifactContentReference content, CancellationToken ct = default);
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
- **Immutability:** writes are conditional creates. Verified content under a
  digest is never replaced. The store reports `AlreadyPresent` only after the
  stored bytes verify against the requested digest and length. A stored object
  that fails verification is corrupt; a verified upload of the same digest
  repairs it, which preserves the content identity.
- **Quarantine survives re-upload:** uploading bytes identical to quarantined
  content reports `AlreadyPresent` and leaves the content in quarantine. It
  never becomes available again through a second upload.
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

### Policy inputs from M0-D16

[M0-D16 #73](https://github.com/bdgrz/compliance/issues/73) was decided by Jeff
Repanich on 2026-09-22. Evidence is retained for 7 years after the period it
supports. An engagement hold blocks disposition until the report is issued
plus 1 year; a legal hold blocks disposition indefinitely. Disposition requires
Org Admin approval. Credentials and secrets must never be stored, so content is
scanned on upload and quarantined when flagged. A quarantined artifact cannot
be downloaded except by an Org Admin, for release or disposal. These values
live in product configuration and records. The storage layer exposes only
the hold, quarantine, and disposition hooks below and encodes no durations or
roles.

### Inspection and quarantine

`IArtifactInspector` is the scan hook. The registered default,
`UninspectedArtifactInspector`, always reports `NotInspected`. The product
never shows content as clean until an inspection engine is selected. Because
M0-D16 requires scanning on upload, EN-06 must not issue ordinary delivery
for an artifact whose inspection state is not `Clean`. The engine choice is
EN-06 work under this hook.

`QuarantineAsync` moves content to the reserved `quarantine/` prefix. Ordinary
`OpenVerifiedAsync` and `IssueDeliveryAsync` then return nothing.
`OpenQuarantinedAsync` serves the release or disposal review, and
`ReleaseQuarantineAsync` returns content to availability. The store does not
decide who may call these. The application permits them only for an Org Admin,
under the owning record's Portia authorizer. In S3 the move is a copy to the
quarantine key followed by deletion of the content key, done by Portia.

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

Storage never expires content on a timer. The 7-year retention period is
evaluated by the application from product records. Content stays until an
approved disposition.

- **Holds:** `PlaceHoldAsync` and `RemoveHoldAsync` record one marker per hold
  record (an engagement hold or a legal hold). As defense in depth, the store
  refuses `DeleteAsync` with `Held` while any marker remains. In S3, Portia
  maps "any hold active" to an Object Lock legal hold on the object.
- **Disposition:** `DeleteAsync` is the only deletion hook. It deletes
  available or quarantined content. The application calls it only after
  retention has elapsed, no hold is active, Org Admin approval is recorded,
  and no live artifact record still references the content. That last check
  also covers a concurrent re-upload, which storage alone cannot serialize.
- The disposition audit record and its workflow are delivered by
  [R2-12](https://github.com/bdgrz/compliance/issues/285). Offboarding export
  and bucket disposition follow the tenant lifecycle in
  [F1-01](https://github.com/bdgrz/compliance/issues/286).

## Spike evidence

`LocalArtifactContentStore` is the development and spike adapter. It maps each
tenant bucket to a tenant directory with the same key layout. It stages content
while hashing, then publishes it with a no-overwrite move. It verifies digest
and length on every open. Its HMAC-signed, expiring delivery locations stand in
for pre-signed URLs. It fails closed when `Artifacts:LocalContentRoot` or
`Artifacts:LocalDeliveryKey` is not configured, and composition rejects a
relative root or a key shorter than 32 bytes. It is not a production store.
It does not sweep staging files left by a killed process.

- `LocalArtifactContentStoreTests` (32 cases) prove conditional create and
  verified `AlreadyPresent`, repair of a corrupt object, no cross-tenant
  deduplication, digest and length verification on read, delivery, and
  deletion, and rejection of non-canonical or path-traversal digests. They also
  cover the content-length limit with no leftover content; bounded,
  unforgeable, and expiring delivery at signed precision; quarantine that
  withholds content and survives re-upload, with release and Org-Admin-path
  review; holds that block disposition until every hold is removed;
  tenant-scoped disposition; fail-closed configuration; and the never-clean
  default inspector.
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
expiry, Object Lock hold mapping, the quarantine move, the selected scan
engine, and the delivery-issued event.

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
