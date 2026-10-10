using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.relationships.list", 1)]
public sealed record ListApplicationRelationships(Uuid TenantId, Uuid ApplicationId,
    string Direction, int Limit = 50, string? Cursor = null)
    : IRequest<Page<ApplicationRelationshipView>>, IApplicationInventoryRequest, ICallable;
