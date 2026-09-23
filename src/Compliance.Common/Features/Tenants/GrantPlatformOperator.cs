using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.platform.operator.grant", 1)]
public sealed record GrantPlatformOperator(Uuid UserId, string Reason)
    : IRequest, ICallable, IPlatformOperatorRequest;
