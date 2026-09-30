using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

static class ServiceIdentityDirectorySchema
{
    public static readonly KvDirectoryIndex<ServiceIdentityView> ByDisplayName = new(
        "by_display_name", 1,
        static view => [view.DisplayName.ToUpperInvariant(), view.ServiceIdentityId.ToString()]);

    public static readonly KvDirectory<ServiceIdentityView, Uuid> Identities = new(
        "service-identities", ComplianceCoreJsonContext.Default.ServiceIdentityView,
        static view => view.ServiceIdentityId,
        static serviceIdentityId => [serviceIdentityId.ToString()], [ByDisplayName]);
}
