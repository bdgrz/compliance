using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.platform.operator.granted", 1)]
public sealed record PlatformOperatorGranted(Uuid ActorUserId, Uuid SubjectUserId,
    DateTimeOffset OccurredAt, string Reason, string? ActorDisplay = null) : DomainEvent;
