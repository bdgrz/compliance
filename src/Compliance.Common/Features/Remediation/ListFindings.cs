using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Lists findings; readiness_status filters closed, remediated, accepted, overdue, or unresolved.</summary>
[Discriminator("bdgrz.findings.list", 1)]
public sealed record ListFindings(Uuid TenantId, Uuid ProgramId, string? ReadinessStatus = null,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<FindingView>>, IProgramReadRequest, ICallable;
