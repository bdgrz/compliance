using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.sod-waiver.record", 1)]
public sealed record RecordSeparationOfDutiesWaiver(
    Uuid TenantId,
    SeparationOfDutiesWaiverScope Scope,
    Uuid BeneficiaryUserId,
    string Rationale,
    DateTimeOffset ExpiresAt)
    : IRequest<SeparationOfDutiesWaiverView>, ISeparationOfDutiesWaiverAdminRequest, ICallable;
