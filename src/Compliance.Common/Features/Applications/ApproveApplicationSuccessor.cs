using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.successor.approve", 1)]
public sealed record ApproveApplicationSuccessor(Uuid TenantId, Uuid PredecessorApplicationId,
    Uuid SuccessorApplicationId, long ExpectedSourceRevision,
    long ExpectedTargetRevision, long ExpectedRelationshipRevision, string ImpactDigest)
    : IRequest, IApplicationInventoryWriteRequest, ICallable;
