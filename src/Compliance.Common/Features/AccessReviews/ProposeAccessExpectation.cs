using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Proposes an expected, required, or prohibited access rule; it has no effect until approved.</summary>
[Discriminator("bdgrz.access_expectation.propose", 1)]
public sealed record ProposeAccessExpectation(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long ExpectedLedgerRevision, string RuleKind,
    AccessExpectationParameters Parameters, string Rationale, DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil = null, Uuid? SupersedesExpectationId = null)
    : IRequest<AccessExpectationView>, IAccessReviewMutationRequest, ICallable;
