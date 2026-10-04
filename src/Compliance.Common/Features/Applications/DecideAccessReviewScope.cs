using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
/// A personal Compliance Lead approval. It is mapped to HTTP only and is not registered as an MCP tool.
/// </summary>
[Discriminator("bdgrz.system_instance.access_review_scope.decide", 1)]
public sealed record DecideAccessReviewScope(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, long ExpectedSystemInstanceRevision, long ExpectedDecisionCount,
    string Decision, string Reason, DateTimeOffset EffectiveFrom,
    DateTimeOffset? ReviewBy = null, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<AccessReviewScopeDecisionView>, IApplicationInventoryWriteRequest,
        IProgramManagementRequest, ICallable;
