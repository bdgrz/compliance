using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Cancels an open request with a rationale.</summary>
[Discriminator("bdgrz.evidence.request.cancel", 1)]
public sealed record CancelEvidenceRequest(Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId,
    long ExpectedRevision, string Rationale)
    : IRequest<EvidenceRequestView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
