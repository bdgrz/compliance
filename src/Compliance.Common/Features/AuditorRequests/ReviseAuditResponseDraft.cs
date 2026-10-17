using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the revise audit response draft intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.auditor-requests.revise-audit-response-draft", 1)]
public sealed record ReviseAuditResponseDraft(Uuid TenantId, Uuid ProgramId, Uuid RequestId, Uuid ResponseId, long ExpectedRevision, string Summary, IReadOnlyList<Uuid> EvidenceArtifactIds) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
