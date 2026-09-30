using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

[Discriminator("bdgrz.workforce.work-relationship.get", 1)]
public sealed record GetWorkRelationship(Uuid TenantId, Uuid RelationshipId,
    long? MinimumRevision = null) : IRequest<WorkRelationshipView>, IWorkforceRequest, ICallable;
