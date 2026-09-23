using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed record SeedPlatformOperatorRoster(Uuid[] UserIds) : IRequest;
