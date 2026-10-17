using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the revise auditor request intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.auditor-requests.revise-auditor-request", 1)]
public sealed record ReviseAuditorRequest(Uuid TenantId, Uuid ProgramId, Uuid RequestId, long ExpectedRevision, string Wording, string Scope, string Priority, DateOnly DueOn, string ChangeReason) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
