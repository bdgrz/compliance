using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Records that an event-driven or ad hoc control performance is due.</summary>
[Discriminator("bdgrz.control.occurrence.open", 1)]
public sealed record OpenControlOccurrence(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string Trigger, DateOnly OccurredOn)
    : IRequest<ControlOccurrenceView>, IControlOperationRequest, IClientManagementMutationRequest, ICallable;
