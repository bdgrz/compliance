using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.canceled", 1)]
public sealed record ApplicationImportCanceled(Uuid TenantId, Uuid BatchId, long Revision,
    string Reason, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset CanceledAt) : DomainEvent;
