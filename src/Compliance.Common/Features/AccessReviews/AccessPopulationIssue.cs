namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     A problem that blocks acceptance. <c>Category</c> is <c>invalid</c>, <c>duplicate</c>,
///     <c>incomplete</c>, or <c>ambiguous</c>; <c>Code</c> is a stable machine reason.
/// </summary>
public sealed record AccessPopulationIssue(string Category, string Code, string Subject,
    string Message);
