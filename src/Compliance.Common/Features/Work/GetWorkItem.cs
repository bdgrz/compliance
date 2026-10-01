using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Reads one visible work item with its attributed assignment and escalation history.</summary>
[Discriminator("bdgrz.work.get", 1)]
public sealed record GetWorkItem(Uuid TenantId, Uuid ProgramId, Uuid WorkItemId)
    : IRequest<WorkItemDetailView>, IProgramReadRequest, ICallable;
