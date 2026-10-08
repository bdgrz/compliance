using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Lists the acting member's visible work in one program, derived from source workflows.
///     <c>Scope</c> is mine (default), team, unassigned, escalated, or all. Work the actor may not
///     act on, assign, or oversee is absent from items and counts. Search optionally matches
///     visible summary, reason, or kind, ignoring case; scoped counts include only matching work.
/// </summary>
[Discriminator("bdgrz.work.list", 1)]
public sealed record ListWork(Uuid TenantId, Uuid ProgramId, string? Scope = null,
    int? HorizonDays = null, string? Search = null) : IRequest<WorkQueueView>, IProgramReadRequest, ICallable;
