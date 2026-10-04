using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.system_instance.retire", 1)]
public sealed record RetireSystemInstance(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long ExpectedRevision, DateTimeOffset EffectiveAt, string Reason)
    : IRequest, IApplicationInventoryWriteRequest, ICallable;
