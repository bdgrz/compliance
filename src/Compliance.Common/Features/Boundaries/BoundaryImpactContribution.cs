namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryImpactContribution(string Context,
    IReadOnlyList<BoundaryAffectedRecord> Records, bool Complete);
