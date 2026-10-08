using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Privacy-safe identity of an exact retained operator-owned directory decision.</summary>
public sealed record DirectoryStatusSourceView(Uuid EventId, Uuid RequestId, long SourceSequence,
    ulong ResourceOffset, string PayloadSha256, Uuid StaffMemberId, Uuid UserId, string Practice,
    long StaffRevision, bool IsActive, DateTimeOffset RecordedAt);
