using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AttestRecords;

/// <summary>Engagement holds still requiring governed delivery or release.</summary>
public sealed record AttestDeliveryHoldView(Uuid TenantId, Uuid ProgramId, Uuid AttestDeliveryHoldId, string Status, string? RecipientReference, DateTimeOffset? DeliveredAt, DateTimeOffset? VerifiedAt);
