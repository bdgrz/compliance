using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.service.recorded", 1)]
public sealed record NonattestServiceRecorded(Uuid TenantId, Uuid RequestId, long ExpectedSequence,
    NonattestServiceView Service) : DomainEvent;
