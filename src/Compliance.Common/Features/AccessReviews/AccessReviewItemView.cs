using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     One frozen review item: a subject's effective access to an entitlement as accepted, with the
///     classification and variance known at launch.
/// </summary>
public sealed record AccessReviewItemView(Uuid ItemId, Uuid PopulationId,
    Uuid PopulationSnapshotId, Uuid SystemInstanceId, Uuid ReviewerMemberId,
    string ProviderSubjectId, string PrincipalKind, string SubjectDisplayName,
    string Classification, Uuid? SubjectPersonId, Uuid? SubjectUserId,
    string ProviderEntitlementId, string EntitlementKind, string EntitlementDisplayName,
    bool Privileged, string VarianceCategory, IReadOnlyList<EffectiveAccessPathView> Paths,
    DateTimeOffset? ExpiresAt);
