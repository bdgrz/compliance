using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the review audit response intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.auditor-requests.review-audit-response", 1)]
public sealed record ReviewAuditResponse(Uuid TenantId, Uuid ProgramId, Uuid RequestId, Uuid ResponseId, long Version, string Outcome, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
