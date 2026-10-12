using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.relationship.list", 1)]
public sealed record ListApplicationRelationships(Uuid TenantId, Uuid ApplicationId,
    int? Limit = null, string? Cursor = null, long? MinimumRevision = null)
    : IRequest<ApplicationRelationshipPage>, IApplicationInventoryRequest, ICallable;
