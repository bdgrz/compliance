using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Claims unassigned team work for the acting team member, who must be eligible under the source workflow.</summary>
[Discriminator("bdgrz.work.claim", 1)]
public sealed record ClaimWorkItem(Uuid TenantId, Uuid ProgramId, Uuid WorkItemId, long ExpectedRevision)
    : IRequest<WorkItemDetailView>, IControlOperationRequest, ICallable;
