using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed record VerifiedFirmProfessionalDutyAssertion(
    string Issuer,
    string Audience,
    Uuid AssertionId,
    Uuid DesignationId,
    Uuid UserId,
    string Duty,
    Uuid? TenantId,
    long AuthorityRevision,
    string Status,
    Uuid? SupersedesAssertionId,
    DateTimeOffset IssuedAt,
    DateTimeOffset NotBefore,
    DateTimeOffset ExpiresAt,
    string EvidenceDigest);
