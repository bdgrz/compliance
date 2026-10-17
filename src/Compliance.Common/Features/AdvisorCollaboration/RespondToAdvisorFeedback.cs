using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the respond to advisor feedback intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.advisor-collaboration.respond-to-advisor-feedback", 1)]
public sealed record RespondToAdvisorFeedback(Uuid TenantId, Uuid ProgramId, Uuid FeedbackId, string Response) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
