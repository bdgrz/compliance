using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record EngagementActualAssignmentView(Uuid StaffMemberId, Uuid UserId, string Practice,
    long DirectoryStaffRevision, DateTimeOffset DirectoryStaffRecordedAt, bool IsCurrent, ActorReference Actor, DateTimeOffset AssignedAt);
