using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Records the create audit response draft intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.auditor-requests.create-audit-response-draft", 1)]
public sealed record CreateAuditResponseDraft(Uuid TenantId, Uuid ProgramId, Uuid RequestId, Uuid ResponseId, string Summary, IReadOnlyList<Uuid> EvidenceArtifactIds) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
