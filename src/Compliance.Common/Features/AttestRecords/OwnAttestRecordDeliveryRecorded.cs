using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AttestRecords;

/// <summary>Records the OwnAttestRecordDeliveryRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.attestrecords.own-attest-record-delivery-recorded", 1)]
public sealed record OwnAttestRecordDeliveryRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
