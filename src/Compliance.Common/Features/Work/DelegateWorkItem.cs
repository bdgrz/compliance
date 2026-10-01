using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>The current assignee hands work to another member the source workflow would accept.</summary>
[Discriminator("bdgrz.work.delegate", 1)]
public sealed record DelegateWorkItem(Uuid TenantId, Uuid ProgramId, Uuid WorkItemId, long ExpectedRevision,
    Uuid AssigneeMemberId, string Reason)
    : IRequest<WorkItemDetailView>, IControlOperationRequest, ICallable;
