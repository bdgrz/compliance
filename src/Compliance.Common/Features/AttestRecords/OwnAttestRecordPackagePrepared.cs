using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AttestRecords;

/// <summary>Records the OwnAttestRecordPackagePrepared domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.attestrecords.own-attest-record-package-prepared", 1)]
public sealed record OwnAttestRecordPackagePrepared(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
