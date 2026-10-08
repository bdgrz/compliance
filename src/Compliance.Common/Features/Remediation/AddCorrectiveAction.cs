using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.corrective_action.add", 1)]
public sealed record AddCorrectiveAction(Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, string Description, Uuid OwnerMemberId, DateOnly DueOn)
    : IRequest<FindingView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
