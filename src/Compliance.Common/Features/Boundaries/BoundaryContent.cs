namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryContent(string Statement, string EngagementStage,
    IReadOnlyList<string> TrustServicesCategories, IReadOnlyList<BoundaryScopeEntry> Entries);
