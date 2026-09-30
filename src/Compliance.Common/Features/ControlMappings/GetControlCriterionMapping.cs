using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

[Discriminator("bdgrz.control_mapping.get", 1)]
public sealed record GetControlCriterionMapping(Uuid TenantId, Uuid ProgramId, Uuid MappingId)
    : IRequest<ControlCriterionMappingView>, IProgramReadRequest, ICallable;
