using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

[Discriminator("bdgrz.evidence.request.get", 1)]
public sealed record GetEvidenceRequest(Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId)
    : IRequest<EvidenceRequestView>, IProgramReadRequest, ICallable;
