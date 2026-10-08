using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>A personal approval of exactly one retained preparation; HTTP only.</summary>
[Discriminator("bdgrz.evidence.redaction.approve", 1)]
public sealed record ApproveEvidenceRedaction(Uuid TenantId, Uuid RedactionId, long ExpectedRevision,
    Uuid PreparationId, long PreparedRevision, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<EvidenceRedactionView>, IEvidenceRedactionMutationRequest, ICallable;
