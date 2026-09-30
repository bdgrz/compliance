using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Lists joiner, mover, and leaver observations derived from roster changes, oldest first.
///     <c>Kind</c> optionally narrows the list to <c>joiner</c>, <c>mover</c>, or <c>leaver</c>.
/// </summary>
[Discriminator("bdgrz.workforce.observation.list", 1)]
public sealed record ListWorkforceObservations(Uuid TenantId, string? Kind = null,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<WorkforceObservationView>>, IWorkforceRequest, ICallable;
