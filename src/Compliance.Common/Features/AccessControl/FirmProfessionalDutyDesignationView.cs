using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record FirmProfessionalDutyDesignationView(Uuid DesignationId, Uuid StaffMemberId, Uuid UserId,
    string Duty, Uuid? TenantId, string SourceReference, long DirectoryStaffRevision, bool IsActive,
    long Revision, ActorReference DesignatedBy, DateTimeOffset DesignatedAt,
    ActorReference LastChangedBy, DateTimeOffset LastChangedAt, string? ChangeReason);
