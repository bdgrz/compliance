using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     A manually attested access population for one exact system-instance revision.
///     <c>Status</c> is <c>draft</c> or <c>accepted</c>. An accepted population is read from its
///     immutable snapshot; its facts can be traced to provider IDs and the snapshot hash.
/// </summary>
public sealed record AccessPopulationView(Uuid TenantId, Uuid PopulationId, long Revision,
    Uuid ApplicationId, Uuid SystemInstanceId, long SystemInstanceRevision,
    DateTimeOffset ObservedAt, string SourceKind, string Source, string Status,
    AccessPopulationFacts Facts, IReadOnlyList<AccessPopulationIssue> Issues,
    IReadOnlyList<EffectiveAccessView> EffectiveAccess, Uuid? CalculationId, Uuid? SnapshotId,
    string? ContentSha256, string? Attestation, ActorReference OpenedBy,
    ActorReference? AcceptedBy, DateTimeOffset? AcceptedAt);
