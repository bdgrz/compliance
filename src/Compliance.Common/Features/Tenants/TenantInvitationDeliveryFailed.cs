using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-invitation.delivery.failed", 1)]
public sealed record TenantInvitationDeliveryFailed(Uuid TenantId, string EmailAddress,
    Uuid DeliveryAttemptId, string FailureCode, DateTimeOffset FailedAt) : DomainEvent;
