using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AttestRecords;

/// <summary>Records the OwnAttestRecordDeliveryVerified domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.attestrecords.own-attest-record-delivery-verified", 1)]
public sealed record OwnAttestRecordDeliveryVerified(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
