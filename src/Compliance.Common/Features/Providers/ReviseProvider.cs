using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Appends authored provider facts at the expected revision.</summary>
[Discriminator("bdgrz.provider.revise", 1)]
public sealed record ReviseProvider(Uuid TenantId, Uuid ProviderId, long ExpectedRevision, ProviderContent Content)
    : IRequest<ProviderRegistration>, IProviderManagementRequest, ICallable;
