using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.sod-waiver.get", 1)]
public sealed record GetSeparationOfDutiesWaiver(Uuid TenantId, Uuid WaiverId)
    : IRequest<SeparationOfDutiesWaiverView>, ISeparationOfDutiesWaiverAdminRequest, ICallable;
