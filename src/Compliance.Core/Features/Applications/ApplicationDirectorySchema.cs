using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationDirectorySchema
{
    public static readonly KvDirectoryIndex<ApplicationView> ByName = new(
        "by_name", 1, static app => [app.Name.ToUpperInvariant(), app.ApplicationId.ToString()]);

    public static readonly KvDirectory<ApplicationView, Uuid> Applications = new(
        "applications", ComplianceCoreJsonContext.Default.ApplicationView,
        static app => app.ApplicationId, static id => [id.ToString()], [ByName]);

    public static readonly KvDirectoryIndex<ApplicationRevisionView> RevisionsByApplication =
        new("by_application", 1, static revision =>
            [revision.ApplicationId.ToString(),
                revision.Revision.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<ApplicationRevisionView, string> Revisions = new(
        "application_revisions", ComplianceCoreJsonContext.Default.ApplicationRevisionView,
        static revision => RevisionKey(revision.ApplicationId, revision.Revision),
        static key => [key], [RevisionsByApplication]);

    public static string RevisionKey(Uuid applicationId, long revision) =>
        $"{applicationId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";

    public static readonly KvDirectoryIndex<SystemInstanceView> ByApplication = new(
        "by_application", 1,
        static instance => [instance.ApplicationId.ToString(), instance.SystemInstanceId.ToString()]);

    public static readonly KvDirectory<SystemInstanceView, Uuid> Instances = new(
        "system_instances", ComplianceCoreJsonContext.Default.SystemInstanceView,
        static instance => instance.SystemInstanceId, static id => [id.ToString()], [ByApplication]);
}
