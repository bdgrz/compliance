using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.advisor-collaboration.list-advisor-requests", 1)]
public sealed record ListAdvisorRequests(Uuid TenantId, Uuid ProgramId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AdvisorRequestSummaryView>>, IProgramReadRequest, ICallable;
