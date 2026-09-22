using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Artifacts;

/// <summary>
/// Vendor-neutral, tenant-scoped immutable content storage. Adapters never deduplicate across tenants.
/// </summary>
public interface IArtifactContentStore
{
    /// <summary>Stores content under its digest unless verified identical content already exists.</summary>
    ValueTask<ArtifactContentWrite> StoreIfAbsentAsync(Uuid tenantId, Stream content,
        CancellationToken ct = default);

    /// <summary>
    /// Opens content only after its digest and length verify. Returns null when the tenant has no such content.
    /// </summary>
    ValueTask<Stream?> OpenVerifiedAsync(ArtifactContentReference content, CancellationToken ct = default);

    /// <summary>
    /// Issues a short-lived delivery location. The caller must already have authorized the actor for the owning
    /// record. Returns null when the tenant has no such content.
    /// </summary>
    ValueTask<ArtifactDelivery?> IssueDeliveryAsync(ArtifactContentReference content, TimeSpan lifetime,
        CancellationToken ct = default);

    /// <summary>
    /// Storage-level disposition hook. The application calls it only after its retention and hold rules permit
    /// deletion. Returns false when the tenant has no such content.
    /// </summary>
    ValueTask<bool> DeleteAsync(ArtifactContentReference content, CancellationToken ct = default);
}
