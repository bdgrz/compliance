using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     How one system instance is reconciled at <c>AsOf</c>. <c>Coverage</c> is <c>covered</c>,
///     <c>excepted</c>, <c>missing_population</c>, <c>not_in_scope</c>, or
///     <c>scope_unresolved</c>. Missing source data is never reported as zero access.
/// </summary>
public sealed record AccessReviewInstanceCoverageView(Uuid SystemInstanceId, string Name,
    string ScopeStatus, string Coverage, Uuid? PopulationId, Uuid? SnapshotId,
    DateTimeOffset? ObservedAt, AccessPopulationExceptionView? Exception);
