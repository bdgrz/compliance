using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record ServiceEngagementStaffProposalView(Uuid StaffMemberId, Uuid UserId, string Practice,
    long DirectoryStaffRevision, string ProposalState, bool IsCurrent, ActorReference Actor, DateTimeOffset RecordedAt, bool ProfessionalAccessGranted);
