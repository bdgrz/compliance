using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Accepts or rejects the pending not-applicable proposal at its exact revision; HTTP-only.
///     The proposer may review only under an approved waiver scoped to
///     (criterion_applicability, decision_id, decision_id, expected_revision, review).
/// </summary>
[Discriminator("bdgrz.criterion_applicability.review", 1)]
public sealed record ReviewCriterionApplicability(Uuid TenantId, Uuid ProgramId,
    Uuid DecisionId, long ExpectedRevision, string Outcome, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null) : IRequest, IProgramScopedRequest, ICallable;
