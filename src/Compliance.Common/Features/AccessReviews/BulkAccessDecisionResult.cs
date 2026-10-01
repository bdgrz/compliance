namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The decisions a bulk request recorded, one per eligible item.</summary>
public sealed record BulkAccessDecisionResult(IReadOnlyList<AccessDecisionView> Decisions);
