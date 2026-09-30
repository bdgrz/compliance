using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application.retire", 1)]
public sealed record RetireApplication(Uuid TenantId, Uuid ApplicationId, long ExpectedRevision,
    DateTimeOffset EffectiveAt, string Reason, Uuid? MergedIntoApplicationId = null)
    : IRequest, IApplicationInventoryRequest, ICallable;
