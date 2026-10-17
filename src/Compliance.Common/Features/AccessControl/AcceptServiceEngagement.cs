using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Records the accept service engagement intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.access-control.accept-service-engagement", 1)]
public sealed record AcceptServiceEngagement(Uuid TenantId, Uuid EngagementId, string AuthorityReference, string? PartnerEvaluationReference, Uuid ManagementAcknowledgementId, Uuid? BoundaryApprovalDecisionId) : IRequest, IClientManagementMutationRequest, ICallable;
