using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the resolve auditor request intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.auditor-requests.resolve-auditor-request", 1)]
public sealed record ResolveAuditorRequest(Uuid TenantId, Uuid ProgramId, Uuid RequestId, string Resolution) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
