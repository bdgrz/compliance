using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Answers an open request with a captured evidence artifact; the owner or a program manager may fulfil it.</summary>
[Discriminator("bdgrz.evidence.request.fulfil", 1)]
public sealed record FulfilEvidenceRequest(Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId,
    long ExpectedRevision, Uuid ArtifactId)
    : IRequest<EvidenceRequestView>, IControlOperationRequest, ICallable;
