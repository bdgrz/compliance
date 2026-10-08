using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

public sealed record ArtifactRetentionView(ArtifactRetentionSource Source, long Revision, string? Policy,
    DateOnly? PeriodStart, DateOnly? PeriodEnd, DateOnly? RetainsThrough, bool RetentionElapsed,
    int ActiveLegalHoldCount, int LegalHoldHistoryCount, bool DispositionAllowed, IReadOnlyList<string> Blockers);
