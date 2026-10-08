using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record FirmStaffMemberView(Uuid StaffMemberId, Uuid UserId, string Practice,
    string SourceReference, bool IsActive, long Revision, ActorReference Actor, DateTimeOffset RecordedAt);
