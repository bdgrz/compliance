using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>An authored historical fact, not an approved ProviderVersion.</summary>
public sealed record ProviderView(Uuid TenantId, Uuid ProviderId, long Revision,
    ProviderContent Content, string SourceKind, string Lifecycle,
    IReadOnlyList<string> Unresolved, ActorReference RecordedBy, DateTimeOffset RecordedAt);
