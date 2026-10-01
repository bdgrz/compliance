using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>An accepted population explained by the expectations approved and effective at its observation.</summary>
public sealed record AccessVarianceView(Uuid TenantId, Uuid PopulationId, Uuid SnapshotId,
    Uuid CalculationId, DateTimeOffset ObservedAt, IReadOnlyList<AccessVarianceItemView> Items);
