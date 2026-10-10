using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record FirmProfessionalDutyDesignationView(Uuid DesignationId, Uuid StaffMemberId, Uuid UserId,
    string Duty, Uuid? TenantId, string SourceReference, long DirectoryStaffRevision, bool IsActive,
    long Revision, ActorReference DesignatedBy, DateTimeOffset DesignatedAt,
    ActorReference LastChangedBy, DateTimeOffset LastChangedAt, string? ChangeReason,
    string AuthorityIssuer = "", Uuid AssertionId = default, long AuthorityRevision = 0,
    DateTimeOffset AssertionIssuedAt = default, DateTimeOffset NotBefore = default,
    DateTimeOffset ExpiresAt = default, string EvidenceDigest = "", string AuthorityStatus = "",
    Uuid? SupersedesAssertionId = null);
