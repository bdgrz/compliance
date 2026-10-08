using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Asks a member for one piece of evidence by a due date, optionally for one control.</summary>
[Discriminator("bdgrz.evidence.request.open", 1)]
public sealed record OpenEvidenceRequest(Uuid TenantId, Uuid ProgramId, string Title, string Instructions,
    Uuid OwnerMemberId, DateOnly DueOn, Uuid? ControlId = null)
    : IRequest<EvidenceRequestView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
