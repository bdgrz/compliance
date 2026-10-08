using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationImportProgress(Uuid TenantId, Uuid BatchId, long Revision, string State,
    int PlannedCount, int DurableEffectCount, int AppliedCount, string? FailureCode, Uuid? FailedRowId,
    DateTimeOffset? AcceptedAt, DateTimeOffset? CommittedAt);
