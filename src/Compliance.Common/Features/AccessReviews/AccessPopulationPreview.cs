using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The issues and derived effective access of the current draft. <c>CanAccept</c> is true
///     only when no issue remains.
/// </summary>
public sealed record AccessPopulationPreview(Uuid PopulationId, long Revision,
    IReadOnlyList<AccessPopulationIssue> Issues, IReadOnlyList<EffectiveAccessView> EffectiveAccess,
    bool CanAccept);
