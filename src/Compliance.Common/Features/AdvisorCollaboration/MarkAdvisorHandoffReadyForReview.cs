using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the mark advisor handoff ready for review intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.advisor-collaboration.mark-advisor-handoff-ready-for-review", 1)]
public sealed record MarkAdvisorHandoffReadyForReview(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, long ExpectedRevision) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
