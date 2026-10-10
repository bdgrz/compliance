using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.firm.professional-duty.designation-revoked", 1)]
public sealed record FirmProfessionalDutyDesignationRevoked(Uuid RequestId, long ExpectedSequence,
    Uuid DesignationId, ActorReference Actor, string Reason, DateTimeOffset RecordedAt) : DomainEvent;
