using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the assign auditor request intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.auditor-requests.assign-auditor-request", 1)]
public sealed record AssignAuditorRequest(Uuid TenantId, Uuid ProgramId, Uuid RequestId, Uuid OwnerMemberId, DateOnly DueOn, string AssignmentReason) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
