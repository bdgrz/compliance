using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AttestRecords;

/// <summary>Attributed delivery and verification receipt.</summary>
public sealed record OwnAttestRecordDeliveryView(Uuid TenantId, Uuid ProgramId, Uuid OwnAttestRecordDeliveryId, string Status, string? RecipientReference, DateTimeOffset? DeliveredAt, DateTimeOffset? VerifiedAt);
