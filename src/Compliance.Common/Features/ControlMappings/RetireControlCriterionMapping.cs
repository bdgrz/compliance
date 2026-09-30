using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>Withdraws an accepted mapping from coverage while keeping its full history.</summary>
[Discriminator("bdgrz.control_mapping.retire", 1)]
public sealed record RetireControlCriterionMapping(Uuid TenantId, Uuid ProgramId,
    Uuid MappingId, long ExpectedRevision, string Rationale)
    : IRequest, IProgramScopedRequest, ICallable;
