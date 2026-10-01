using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed record CampaignWaiverView(Uuid WaiverId, Uuid PersonId, string Reason,
    DateOnly ExpiresOn, ActorReference Approver, DateTimeOffset ApprovedAt);
