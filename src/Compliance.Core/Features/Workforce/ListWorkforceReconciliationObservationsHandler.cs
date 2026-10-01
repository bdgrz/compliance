using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class ListWorkforceReconciliationObservationsHandler(
    WorkforceRosterReconciler reconciler, WorkforceObservationResolutions resolutions)
    : IRequestHandler<ListWorkforceReconciliationObservations,
        Page<WorkforceReconciliationObservationView>>
{
    static readonly string[] Kinds = ["missing", "duplicate", "conflicting", "stale", "access_only"];
    static readonly string[] Statuses = ["open", "resolved", "dismissed"];

    public async ValueTask<Result<Page<WorkforceReconciliationObservationView>>> HandleAsync(
        IRequestContext<ListWorkforceReconciliationObservations> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Invalid("The reconciliation observation list limit must be between 1 and 200.");
        if (request.Kind is { } kind && !Kinds.Contains(kind))
            return Invalid(
                "The observation kind must be missing, duplicate, conflicting, stale, or access_only.");
        if (request.Status is { } status && !Statuses.Contains(status))
            return Invalid("The observation status must be open, resolved, or dismissed.");
        Uuid? after = null;
        if (request.Cursor is { } cursor)
        {
            if (!Uuid.TryParse(cursor, out var parsed))
                return Invalid("The reconciliation observation cursor is invalid.");
            after = parsed;
        }
        var evaluated = await reconciler.EvaluateAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!evaluated.IsSuccess)
            return Result<Page<WorkforceReconciliationObservationView>>.Failure(evaluated.Error);
        var closures = await resolutions.LoadAsync(request.TenantId,
            [.. evaluated.Value.Select(static item => item.ObservationId)], ct).ConfigureAwait(false);
        if (!closures.IsSuccess)
            return Result<Page<WorkforceReconciliationObservationView>>.Failure(closures.Error);
        var afterKey = after?.ToString();
        var matching = evaluated.Value
            .Select(item =>
            {
                var closure = closures.Value.GetValueOrDefault(item.ObservationId);
                return item with
                {
                    Status = WorkforceObservationStatus.Of(closure),
                    Resolution = closure,
                };
            })
            .Where(item => request.Kind is null || item.Kind == request.Kind)
            .Where(item => request.Status is null || item.Status == request.Status)
            .Where(item => afterKey is null ||
                           string.CompareOrdinal(item.ObservationId.ToString(), afterKey) > 0)
            .ToList();
        var take = request.Limit ?? 50;
        var items = matching.Take(take).ToList();
        return Result<Page<WorkforceReconciliationObservationView>>.Success(
            new Page<WorkforceReconciliationObservationView>(items,
                matching.Count > take ? items[^1].ObservationId.ToString() : null));
    }

    static Result<Page<WorkforceReconciliationObservationView>> Invalid(string message) =>
        Result<Page<WorkforceReconciliationObservationView>>.Failure(
            new RequestError(RequestErrorKind.Validation, message));
}
