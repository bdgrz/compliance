using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

[Discriminator("bdgrz.evidence.redaction.get", 1)]
public sealed record GetEvidenceRedaction(Uuid TenantId, Uuid RedactionId)
    : IRequest<EvidenceRedactionView>, ICallable;
