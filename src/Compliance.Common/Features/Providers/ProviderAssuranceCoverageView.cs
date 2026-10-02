using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     A calculated reading of recorded facts, never an approval or readiness gap (#489 owns
///     readiness rules). It carries no sensitive report text, so every reader sees the same view.
///     Status is one of <c>not_required</c>, <c>unresolved_materiality</c>, <c>no_review</c>,
///     <c>not_acceptable</c>, <c>review_overdue</c>, <c>evidence_unrecorded</c>,
///     <c>evidence_stale</c> or <c>current</c>. <c>current</c> means a review is in force on
///     evidence inside its annual cycle; <c>Complete</c> is stricter and true only when the recorded
///     assurance period itself contains <c>AsOf</c> and nothing in the report or review is open.
///     <c>Reasons</c> name every open point, including a bridged-only, expired or unbridged interval.
/// </summary>
public sealed record ProviderAssuranceCoverageView(Uuid TenantId, Uuid ProviderId, long ProviderRevision,
    DateOnly AsOf, string? Materiality, string Status, bool Complete, IReadOnlyList<string> Reasons,
    Uuid? LatestReviewId, DateOnly? LatestReviewedAt, DateOnly? NextReviewDue, string? LatestConclusion,
    IReadOnlyList<AssuranceReportCoverageView> Reports);
