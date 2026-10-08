using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

public sealed record ArtifactLegalHoldView(Uuid HoldId, string Reason,
    Bdgrz.Compliance.Features.AccessControl.ActorReference PlacedBy, DateTimeOffset PlacedAt,
    string? ReleaseReason = null, Bdgrz.Compliance.Features.AccessControl.ActorReference? ReleasedBy = null,
    DateTimeOffset? ReleasedAt = null);
