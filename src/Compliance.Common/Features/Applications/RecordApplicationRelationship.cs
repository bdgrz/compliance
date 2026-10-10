using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.relationship.record", 1)]
public sealed record RecordApplicationRelationship(Uuid TenantId, Uuid SourceApplicationId,
    Uuid TargetApplicationId, string RelationshipType)
    : IRequest<ApplicationRelationshipRegistration>, IApplicationInventoryWriteRequest, ICallable;
