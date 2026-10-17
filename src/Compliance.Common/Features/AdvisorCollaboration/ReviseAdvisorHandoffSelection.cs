using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the revise advisor handoff selection intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.advisor-collaboration.revise-advisor-handoff-selection", 1)]
public sealed record ReviseAdvisorHandoffSelection(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, long ExpectedRevision, IReadOnlyList<Uuid> RecordVersionIds, string ChangeReason) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
