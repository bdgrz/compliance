using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the correct advisor feedback intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.advisor-collaboration.correct-advisor-feedback", 1)]
public sealed record CorrectAdvisorFeedback(Uuid TenantId, Uuid ProgramId, Uuid FeedbackId, string CorrectedBody, string CorrectionReason) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
