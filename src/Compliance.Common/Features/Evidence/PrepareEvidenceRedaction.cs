using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

[Discriminator("bdgrz.evidence.redaction.prepare", 1)]
public sealed record PrepareEvidenceRedaction(Uuid TenantId, Uuid RedactionId, long ExpectedRevision,
    Uuid OriginalArtifactId, Uuid DerivedArtifactId, string Provenance, string Reason)
    : IRequest<EvidenceRedactionView>, IEvidenceRedactionMutationRequest, ICallable;
