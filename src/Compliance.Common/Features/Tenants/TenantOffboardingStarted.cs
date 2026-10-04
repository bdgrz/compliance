using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.offboarding.started", 1)]
public sealed record TenantOffboardingStarted(Uuid TenantId, Uuid OperatorUserId, string Reason,
    DateTimeOffset StartedAt) : DomainEvent;
