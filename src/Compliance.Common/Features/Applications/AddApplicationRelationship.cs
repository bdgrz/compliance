using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.relationship.add", 1)]
public sealed record AddApplicationRelationship(Uuid TenantId, Uuid ApplicationId,
    Uuid RelationshipId, long ExpectedRelationshipRevision, Uuid TargetApplicationId,
    string RelationshipType) : IRequest<ApplicationRelationshipChange>,
    IApplicationInventoryWriteRequest, ICallable;
