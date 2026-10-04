using Bdgrz.Compliance.Features.Providers;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Provider facts, reviews and source gaps available to one as-of readiness run.</summary>
public sealed record ReadinessProviderInput(ProviderView Provider,
    IReadOnlyList<AssuranceReportView> Reports, IReadOnlyList<ProviderReviewView> Reviews,
    IReadOnlyList<ProviderCoverageGapView>? CoverageGaps = null);
