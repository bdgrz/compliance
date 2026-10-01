using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Lists missing, duplicate, conflicting, stale, and access-only roster observations, ordered by
///     observation ID. <c>Kind</c> and <c>Status</c> (<c>open</c>, <c>resolved</c>,
///     <c>dismissed</c>) optionally narrow the list.
/// </summary>
[Discriminator("bdgrz.workforce.reconciliation.list", 1)]
public sealed record ListWorkforceReconciliationObservations(Uuid TenantId, string? Kind = null,
    string? Status = null, int? Limit = null, string? Cursor = null)
    : IRequest<Page<WorkforceReconciliationObservationView>>, IWorkforceRequest, ICallable;
