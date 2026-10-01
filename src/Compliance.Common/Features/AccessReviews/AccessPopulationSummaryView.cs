using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A population's identity and status, without its facts.</summary>
public sealed record AccessPopulationSummaryView(Uuid TenantId, Uuid PopulationId,
    Uuid ApplicationId, Uuid SystemInstanceId, long SystemInstanceRevision,
    DateTimeOffset ObservedAt, long Revision, string Status, Uuid? SnapshotId,
    string? ContentSha256, DateTimeOffset? AcceptedAt);
