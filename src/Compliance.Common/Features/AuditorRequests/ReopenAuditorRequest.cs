using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the reopen auditor request intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.auditor-requests.reopen-auditor-request", 1)]
public sealed record ReopenAuditorRequest(Uuid TenantId, Uuid ProgramId, Uuid RequestId, string ReopenReason) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
