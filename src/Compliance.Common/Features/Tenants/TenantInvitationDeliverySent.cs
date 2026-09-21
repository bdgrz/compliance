using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-invitation.delivery.sent", 1)]
public sealed record TenantInvitationDeliverySent(Uuid TenantId, string EmailAddress,
    Uuid DeliveryAttemptId, DateTimeOffset SentAt) : DomainEvent;
