using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Internal source discovery only; neither membership nor professional authority.</summary>
sealed record ActualStaffEngagementLocatorView(Uuid LocatorId, Uuid TenantId, Uuid EngagementId,
    Uuid AcceptanceRequestId, long AcceptanceRevision, long AcceptanceSourceSequence,
    string AcceptanceSha256, string SourcePayloadSha256, Uuid StaffMemberId, Uuid UserId, string Practice,
    long DirectoryStaffRevision, DateTimeOffset AcceptedAt, DateTimeOffset AssignedAt,
    string SourceRealm, string SourceArea, string SourceResource,
    ulong SourceResourceOffset, string SourceNextCursor);
