using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the record advisor feedback intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.advisor-collaboration.record-advisor-feedback", 1)]
public sealed record RecordAdvisorFeedback(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, Uuid RecordVersionId, string Kind, string Body) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
