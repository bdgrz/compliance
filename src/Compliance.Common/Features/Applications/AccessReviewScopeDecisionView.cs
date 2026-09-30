using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record AccessReviewScopeDecisionView(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long SystemInstanceRevision, Uuid DecisionId, long Sequence,
    string Decision, string Reason, DateTimeOffset EffectiveFrom, DateTimeOffset? ReviewBy,
    ActorReference ApprovedBy, DateTimeOffset DecidedAt, Uuid? SeparationOfDutiesWaiverId);
