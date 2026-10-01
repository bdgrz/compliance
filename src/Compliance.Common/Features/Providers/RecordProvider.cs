using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Records authored provider facts; this grants no assurance or scope approval.</summary>
[Discriminator("bdgrz.provider.record", 1)]
public sealed record RecordProvider(Uuid TenantId, ProviderContent Content)
    : IRequest<ProviderRegistration>, IProviderManagementRequest, ICallable;
