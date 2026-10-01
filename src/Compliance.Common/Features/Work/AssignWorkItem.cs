using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>A program manager assigns or reassigns work to a member the source workflow would accept.</summary>
[Discriminator("bdgrz.work.assign", 1)]
public sealed record AssignWorkItem(Uuid TenantId, Uuid ProgramId, Uuid WorkItemId, long ExpectedRevision,
    Uuid AssigneeMemberId, string? Reason = null)
    : IRequest<WorkItemDetailView>, IControlOperationRequest, ICallable;
