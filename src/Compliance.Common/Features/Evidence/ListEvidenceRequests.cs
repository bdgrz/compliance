using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Lists the program's evidence requests in the order opened; status filters open, fulfilled, or cancelled.</summary>
[Discriminator("bdgrz.evidence.requests.list", 1)]
public sealed record ListEvidenceRequests(Uuid TenantId, Uuid ProgramId, string? Status = null,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<EvidenceRequestView>>, IProgramReadRequest, ICallable;
