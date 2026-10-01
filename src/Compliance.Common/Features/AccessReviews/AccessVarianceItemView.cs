using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     One explained observation. <c>Category</c> is <c>expected</c>, <c>unexpected</c>,
///     <c>prohibited</c>, <c>missing</c>, <c>unresolved</c>, or <c>excepted</c>. A category explains
///     the population against approved expectations; it never decides a review outcome.
/// </summary>
public sealed record AccessVarianceItemView(string Category, string ProviderSubjectId,
    string? ProviderEntitlementId, bool Privileged, IReadOnlyList<AccessVarianceFinding> Findings);
