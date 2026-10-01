using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class AccessReviewScopeStreams
{
    public const string Area = "system-instance-access-review-scopes";
    public const string ProjectorName = "AccessReviewScopeDirectoryV1";

    public static EventStreamPattern TenantPattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), Area);
}
