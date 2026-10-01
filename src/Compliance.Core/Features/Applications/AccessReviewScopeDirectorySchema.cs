using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class AccessReviewScopeDirectorySchema
{
    public static readonly KvDirectory<AccessReviewScopeRecord, Uuid> Scopes = new(
        "access_review_scopes", ComplianceCoreJsonContext.Default.AccessReviewScopeRecord,
        static scope => scope.SystemInstanceId, static id => [id.ToString()], []);
}
