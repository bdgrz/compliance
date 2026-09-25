using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

[Discriminator("bdgrz.boundary.decision.get", 1)]
public sealed record GetBoundaryDecision(Uuid TenantId, Uuid BoundaryId, Uuid DecisionId)
    : IRequest<BoundaryDecisionView>, ITenantAccessRequest, ICallable;
