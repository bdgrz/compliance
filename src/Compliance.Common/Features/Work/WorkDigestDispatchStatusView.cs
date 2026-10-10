using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Redacted delivery state for one tenant member's weekly digest.</summary>
public sealed record WorkDigestDispatchStatusView(Uuid TenantId, Uuid MemberId,
    DateOnly WeekOf, string TimeZoneId, DateTimeOffset ScheduledAt, string Status,
    int Attempts, DateTimeOffset? LastAttemptAt, Uuid MessageId,
    DateTimeOffset LastUpdatedAt, DateTimeOffset? NextAttemptAt, string? FailureCode,
    Uuid? RetryAuthorizedBy = null, DateTimeOffset? RetryAuthorizedAt = null,
    string? RetryEvidenceReference = null);
