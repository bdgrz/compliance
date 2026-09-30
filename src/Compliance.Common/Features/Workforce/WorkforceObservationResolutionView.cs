using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     The attributed closure of one workforce observation. <c>Resolution</c> is <c>resolved</c>
///     (the underlying work was done) or <c>dismissed</c> (reviewed and accepted as not needing work).
/// </summary>
public sealed record WorkforceObservationResolutionView(Uuid TenantId, Uuid ObservationId,
    string Resolution, string Note, ActorReference ResolvedBy, DateTimeOffset ResolvedAt);
