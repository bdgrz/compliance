using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.advisor-collaboration.list-advisor-feedback", 1)]
public sealed record ListAdvisorFeedback(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AdvisorFeedbackView>>, IProgramReadRequest, ICallable;
