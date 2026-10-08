using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record ServiceEngagementBoundaryReference(Uuid BoundaryId, Uuid VersionId, long Revision);
