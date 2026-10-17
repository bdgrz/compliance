using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.auditor-requests.list-auditor-requests", 1)]
public sealed record ListAuditorRequests(Uuid TenantId, Uuid ProgramId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AuditorRequestSummaryView>>, IProgramReadRequest, ICallable;
