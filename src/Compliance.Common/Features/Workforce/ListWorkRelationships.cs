using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Lists work relationships ordered by source worker ID, with restricted fields redacted.</summary>
[Discriminator("bdgrz.workforce.work-relationship.list", 1)]
public sealed record ListWorkRelationships(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<WorkRelationshipView>>, IWorkforceRequest, ICallable;
