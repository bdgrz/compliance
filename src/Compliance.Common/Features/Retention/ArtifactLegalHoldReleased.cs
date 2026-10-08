using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

[Discriminator("bdgrz.artifact_retention.legal_hold_released", 1)]
public sealed record ArtifactLegalHoldReleased(ArtifactRetentionSource Source, long Revision, ulong SourcePosition,
    Uuid HoldId, string Reason, Bdgrz.Compliance.Features.AccessControl.ActorReference RecordedBy, DateTimeOffset RecordedAt) : DomainEvent;
