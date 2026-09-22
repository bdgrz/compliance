using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Artifacts;

/// <summary>
/// Vendor-neutral, tenant-scoped immutable content storage. Adapters never deduplicate across tenants. Retention,
/// hold, disposition, and quarantine policy live in product records; this port only exposes their storage hooks.
/// </summary>
public interface IArtifactContentStore
{
    /// <summary>
    /// Stores content under its digest unless verified identical content already exists. A stored object that fails
    /// verification is replaced by the verified upload.
    /// </summary>
    ValueTask<ArtifactContentWrite> StoreIfAbsentAsync(Uuid tenantId, Stream content,
        CancellationToken ct = default);

    /// <summary>
    /// Opens available content only after its digest and length verify. Returns null when the tenant has no such
    /// available content, including content in quarantine.
    /// </summary>
    ValueTask<Stream?> OpenVerifiedAsync(ArtifactContentReference content, CancellationToken ct = default);

    /// <summary>
    /// Issues a short-lived delivery location for available content. The caller must already have authorized the
    /// actor for the owning record. Returns null when the tenant has no such available content.
    /// </summary>
    ValueTask<ArtifactDelivery?> IssueDeliveryAsync(ArtifactContentReference content, TimeSpan lifetime,
        CancellationToken ct = default);

    /// <summary>
    /// Storage-level hold hook. Content with any active hold cannot be deleted. Returns false when the tenant has no
    /// such content.
    /// </summary>
    ValueTask<bool> PlaceHoldAsync(ArtifactContentReference content, Uuid holdId, CancellationToken ct = default);

    /// <summary>Removes one hold. Returns false when that hold was not active.</summary>
    ValueTask<bool> RemoveHoldAsync(ArtifactContentReference content, Uuid holdId, CancellationToken ct = default);

    /// <summary>
    /// Storage-level quarantine hook. Quarantined content is withheld from ordinary reads and delivery. Returns false
    /// when the tenant has no such available content.
    /// </summary>
    ValueTask<bool> QuarantineAsync(ArtifactContentReference content, CancellationToken ct = default);

    /// <summary>Returns quarantined content to availability. Returns false when it is not in quarantine.</summary>
    ValueTask<bool> ReleaseQuarantineAsync(ArtifactContentReference content, CancellationToken ct = default);

    /// <summary>
    /// Opens quarantined content for an authorized release or disposal review. Returns null when it is not in
    /// quarantine.
    /// </summary>
    ValueTask<Stream?> OpenQuarantinedAsync(ArtifactContentReference content, CancellationToken ct = default);

    /// <summary>
    /// Storage-level disposition hook. The application calls it only after its retention rules and disposition
    /// approval permit deletion. The store refuses deletion while any hold is active.
    /// </summary>
    ValueTask<ArtifactDeletionResult> DeleteAsync(ArtifactContentReference content, CancellationToken ct = default);
}
