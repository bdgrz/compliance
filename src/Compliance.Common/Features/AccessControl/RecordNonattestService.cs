using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.service.record", 1)]
public sealed record RecordNonattestService(Uuid TenantId, Uuid ServiceRecordId,
    long ExpectedSequence, NonattestServiceContent Content) : IRequest<NonattestServiceView>,
    IIndependenceAdministrationRequest, ICallable;
