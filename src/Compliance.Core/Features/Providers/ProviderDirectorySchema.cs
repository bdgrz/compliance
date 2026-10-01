using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

static class ProviderDirectorySchema
{
    public static readonly KvDirectoryIndex<ProviderView> ByName = new("by_name", 1,
        static view => [view.Content.Name.ToUpperInvariant(), view.ProviderId.ToString()]);

    public static readonly KvDirectory<ProviderView, Uuid> Providers = new("providers",
        ComplianceCoreJsonContext.Default.ProviderView, static view => view.ProviderId,
        static id => [id.ToString()], [ByName]);

    public static readonly KvDirectoryIndex<ProviderView> ByProvider = new("by_provider", 1,
        static view => [view.ProviderId.ToString(), view.Revision.ToString("D20", CultureInfo.InvariantCulture)]);

    public static readonly KvDirectory<ProviderView, string> Revisions = new("provider_revisions",
        ComplianceCoreJsonContext.Default.ProviderView, static view => RevisionKey(view.ProviderId, view.Revision),
        static key => [key], [ByProvider]);

    public static string RevisionKey(Uuid providerId, long revision) =>
        $"{providerId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}";
}
