using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     A reviewer's personal due-diligence conclusion (M0-D11 <c>ProviderReview</c>). Evidence is a
///     same-provider assurance report of the same kind, or a classified citation for evidence kept
///     outside the register. The reviewer is the authenticated actor, never a supplied name.
/// </summary>
public sealed record ProviderReviewContent(DateOnly ReviewedAt, DateOnly NextReviewDue, string EvidenceKind,
    string Conclusion, string Rationale, Uuid? AssuranceReportId = null,
    IReadOnlyList<string>? Exceptions = null, ProviderSourceCitation? Evidence = null);
