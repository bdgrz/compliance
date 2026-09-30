using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>Accepts or rejects the pending proposal of one mapping at its exact revision.</summary>
[Discriminator("bdgrz.control_mapping.review", 1)]
public sealed record ReviewControlCriterionMapping(Uuid TenantId, Uuid ProgramId,
    Uuid MappingId, long ExpectedRevision, string Outcome, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null) : IRequest, IProgramScopedRequest, ICallable;
