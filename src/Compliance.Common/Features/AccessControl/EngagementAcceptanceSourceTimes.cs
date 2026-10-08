namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record EngagementAcceptanceSourceTimes(DateTimeOffset PartnerDirectoryRecordedAt,
    DateTimeOffset PartnerAuthorityVerifiedAt, DateTimeOffset? BoundaryChangedAt, DateTimeOffset? BoundaryApprovedAt);
