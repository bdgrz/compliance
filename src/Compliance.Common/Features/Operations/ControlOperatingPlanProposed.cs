using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

[Discriminator("bdgrz.control.operating_plan.proposed", 1)]
public sealed record ControlOperatingPlanProposed(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long Revision, ControlOperatingPlanView Plan) : DomainEvent;
