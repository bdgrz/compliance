using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Lists findings with their readiness status evaluated at read time.</summary>
public sealed class ListFindingsHandler(IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<ListFindings, Page<FindingView>>
{
    public async ValueTask<Result<Page<FindingView>>> HandleAsync(
        IRequestContext<ListFindings> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.ReadinessStatus is not (null or RemediationLedger.Closed or
            RemediationLedger.Remediated or RemediationLedger.Accepted or
            RemediationLedger.Overdue or RemediationLedger.Unresolved))
            return Result<Page<FindingView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The readiness status filter must be closed, remediated, accepted, overdue, or unresolved."));
        var ledger = await reader.HydrateAsync(new RemediationLedger(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        var items = ledger.ReadAll(clock.GetUtcNow()).Where(finding =>
            request.ReadinessStatus is null || finding.ReadinessStatus == request.ReadinessStatus)
            .ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor, "findings");
    }
}
