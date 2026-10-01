using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The immutable snapshot an accepted population was frozen as, and its effective-access calculation.</summary>
public sealed record AccessPopulationAcceptance(Uuid PopulationId, Uuid SnapshotId,
    string ContentSha256, Uuid CalculationId);
