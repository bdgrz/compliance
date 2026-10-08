using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     The acting member's personal Type I entry sign-off on the latest readiness assessment:
///     approve, approve_with_exceptions, or defer. Every unresolved gap must be explicitly
///     acknowledged to approve with exceptions. HTTP-only; never an MCP tool.
/// </summary>
[Discriminator("bdgrz.readiness.type_i_entry.decide", 1)]
public sealed record DecideTypeIEntry(Uuid TenantId, Uuid ProgramId, Uuid AssessmentId,
    long ExpectedRevision, string Outcome, string Rationale,
    IReadOnlyList<Uuid>? AcknowledgedGapIds = null, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<TypeIEntryDecisionView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
