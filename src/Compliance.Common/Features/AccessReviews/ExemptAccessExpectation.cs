using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     A personal approval of an expiring exception to an approved expectation. HTTP-only; it is
///     not registered as an MCP tool.
/// </summary>
[Discriminator("bdgrz.access_expectation.exception.record", 1)]
public sealed record ExemptAccessExpectation(Uuid TenantId, Uuid SystemInstanceId,
    Uuid ExpectationId, long ExpectedLedgerRevision, string ProviderSubjectId,
    string Rationale, DateTimeOffset ExpiresAt, string? ProviderEntitlementId = null)
    : IRequest<AccessExpectationExceptionView>, IAccessReviewRequest, ICallable;
