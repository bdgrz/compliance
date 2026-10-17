using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the record auditor request comment intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.auditor-requests.record-auditor-request-comment", 1)]
public sealed record RecordAuditorRequestComment(Uuid TenantId, Uuid ProgramId, Uuid RequestId, string Body, string Compartment) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
