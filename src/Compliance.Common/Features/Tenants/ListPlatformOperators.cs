using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.platform.operator.list", 1)]
public sealed record ListPlatformOperators() : IRequest<PlatformOperatorRosterView>, ICallable,
    IPlatformOperatorRequest;
