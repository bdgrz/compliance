using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     A personal approval of a proposed expectation. The proposer cannot approve it without an
///     exact-scope waiver. HTTP-only; it is not registered as an MCP tool.
/// </summary>
[Discriminator("bdgrz.access_expectation.approve", 1)]
public sealed record ApproveAccessExpectation(Uuid TenantId, Uuid SystemInstanceId,
    Uuid ExpectationId, long ExpectedLedgerRevision, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<AccessExpectationView>, IAccessReviewRequest, ICallable;
