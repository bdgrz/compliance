using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

[Discriminator("bdgrz.control.retire", 1)]
public sealed record RetireControl(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long ExpectedRevision, Uuid AcceptedReviewDecisionId, string ImpactDigest,
    string Rationale, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest, IProgramScopedRequest, ICallable;
