using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     One attributable classification decision. <c>Classification</c> is <c>human</c>,
///     <c>nhi</c>, <c>shared</c>, or <c>unclassified</c>. <c>ProposedClassification</c> is the
///     provider hint that was shown; it never decides.
/// </summary>
public sealed record AccessPrincipalClassificationView(long Sequence, string Classification,
    Uuid? PersonId, Uuid? ServiceIdentityId, Uuid? AccountableOwnerPersonId,
    string? SharedJustification, string Rationale, string? ProposedClassification,
    ActorReference ClassifiedBy, DateTimeOffset ClassifiedAt);
