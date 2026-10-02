using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Criteria;

public sealed record CriteriaTextOverlayMetadata(long Revision, string Supplier, string LicenseReference,
    CriteriaOverlayUsageFlags AllowedUses, ActorReference Actor, DateTimeOffset RecordedAt);
