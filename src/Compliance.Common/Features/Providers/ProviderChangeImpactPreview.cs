using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
/// A bounded, read-only assessment of records linked to a proposed provider lifecycle change.
/// It does not approve the change or claim an atomic cross-stream snapshot.
/// </summary>
public sealed record ProviderChangeImpactPreview(Uuid TenantId, Uuid ProviderId,
    long ProviderRevision, string ChangeKind, DateOnly EffectiveOn, string ChangeSummary,
    IReadOnlyList<ProviderChangeImpactSection> Contexts, IReadOnlyList<string> PendingContexts,
    bool Complete, string Digest);

public sealed record ProviderChangeImpactSection(string Context,
    IReadOnlyList<ProviderChangeAffectedRecord> Records, bool Complete, string? IncompleteReason);

public sealed record ProviderChangeAffectedRecord(string RecordType, Uuid RecordId,
    Uuid? ParentRecordId, long? Revision, string Relationship);
