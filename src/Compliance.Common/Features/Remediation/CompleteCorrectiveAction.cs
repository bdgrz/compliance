using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>The action owner, or a program manager, records the completed corrective work.</summary>
[Discriminator("bdgrz.finding.corrective_action.complete", 1)]
public sealed record CompleteCorrectiveAction(Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, Uuid ActionId, string ResolutionNotes,
    IReadOnlyList<EvidenceReference> Evidence)
    : IRequest<FindingView>, IControlOperationRequest, IClientManagementMutationRequest, ICallable;
