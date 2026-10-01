using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.corrective_action.completed", 1)]
public sealed record CorrectiveActionCompleted(Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, Uuid ActionId, string ResolutionNotes, IReadOnlyList<EvidenceReference> Evidence,
    Uuid CompletedByMemberId, ActorReference CompletedBy, DateTimeOffset CompletedAt)
    : DomainEvent;
