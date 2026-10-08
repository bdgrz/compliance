using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.sod-waiver.approve", 1)]
public sealed record ApproveSeparationOfDutiesWaiver(Uuid TenantId, Uuid WaiverId)
    : IRequest<SeparationOfDutiesWaiverView>, ISeparationOfDutiesWaiverAdminRequest, IClientManagementMutationRequest, ICallable;
