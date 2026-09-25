using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationImportView(Uuid TenantId, Uuid BatchId, Uuid SubmissionId,
    string SourceKey, string SourceNamespace, string Coverage, string ContentSha256,
    long Revision, string State, Uuid SubmittedByMemberId, string SubmittedByDisplay,
    DateTimeOffset SubmittedAt, int RowCount, int InvalidCount, int PendingCount,
    int AppliedCount, int SkippedCount, int FailedCount, DateTimeOffset LastProgressAt);
