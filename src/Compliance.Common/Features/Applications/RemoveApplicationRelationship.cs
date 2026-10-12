using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.relationship.remove", 1)]
public sealed record RemoveApplicationRelationship(Uuid TenantId, Uuid ApplicationId,
    Uuid RelationshipId, long ExpectedRelationshipRevision)
    : IRequest<ApplicationRelationshipChange>, IApplicationInventoryWriteRequest, ICallable;
