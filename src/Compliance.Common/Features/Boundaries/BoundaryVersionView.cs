using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryVersionView(Uuid TenantId, Uuid BoundaryId, Uuid ProgramId,
    Uuid VersionId, long Revision, BoundaryContent Content, string Status,
    DateOnly? EffectiveFrom, Uuid AuthorMemberId, string AuthorDisplay,
    DateTimeOffset ChangedAt);
