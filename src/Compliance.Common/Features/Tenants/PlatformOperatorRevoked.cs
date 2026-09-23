using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.platform.operator.revoked", 1)]
public sealed record PlatformOperatorRevoked(Uuid ActorUserId, Uuid SubjectUserId,
    DateTimeOffset OccurredAt, string Reason) : DomainEvent;
