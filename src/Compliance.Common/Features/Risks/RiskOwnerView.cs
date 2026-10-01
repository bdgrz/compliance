using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     The governed workforce person accountable for a risk. The person need not sign in;
///     <c>CorrelatedMemberId</c> is the platform member the person was correlated with when the
///     owner was assigned, used only for separation of duties and never as an access grant.
/// </summary>
public sealed record RiskOwnerView(Uuid PersonId, Uuid? CorrelatedMemberId, string Rationale,
    ActorReference AssignedBy, DateTimeOffset AssignedAt);
