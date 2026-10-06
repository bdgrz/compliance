using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>An audit effect that remains invisible until its source ledger commits.</summary>
[Discriminator("bdgrz.application_import.effect_pending", 1)]
public sealed record ApplicationImportEffectPending(Uuid TenantId, Uuid ApplicationId,
    long PlanRevision, string PlanSha256, ApplicationImportPlanStarted Plan,
    ApplicationImportPlannedRow Row, DateTimeOffset RecordedAt) : DomainEvent
{
    public ActorReference StoredActor { get; } = ActorReference.ForSystemProcess(
        "reactor:application-import", "application-import");
}
