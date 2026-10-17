using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the submit audit response intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.auditor-requests.submit-audit-response", 1)]
public sealed record SubmitAuditResponse(Uuid TenantId, Uuid ProgramId, Uuid RequestId, Uuid ResponseId, long Version, string SubmissionNote) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
