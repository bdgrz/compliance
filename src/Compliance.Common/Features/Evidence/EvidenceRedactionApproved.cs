using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

[Discriminator("bdgrz.evidence.redaction.approved", 1)]
public sealed record EvidenceRedactionApproved(Uuid TenantId, Uuid RedactionId,
    long ExpectedRevision, EvidenceRedactionApprovalFact Approval) : DomainEvent;
