using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

public sealed record ArtifactRetentionSourceSnapshot(ArtifactRetentionSource Source, ulong Position,
    DateOnly? PeriodStart = null, DateOnly? PeriodEnd = null);
