using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Platform-side verification from a later accepted population. <c>Outcome</c> is
///     <c>removed</c> or <c>changed</c>.
/// </summary>
public sealed record AccessRemediationVerificationView(Uuid PopulationId, Uuid SnapshotId,
    Uuid CalculationId, DateTimeOffset ObservedAt, string Outcome, ActorReference VerifiedBy,
    DateTimeOffset VerifiedAt);
