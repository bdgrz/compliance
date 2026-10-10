using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records a platform operator's evidence-backed same-week retry authorization.</summary>
public sealed class AuthorizeWorkDigestUnknownRetryHandler(IAggregateExecutor executor,
    TimeProvider clock, WorkDigestDeliverySettings settings)
    : IRequestHandler<AuthorizeWorkDigestUnknownRetry, WorkDigestDispatchStatusView>
{
    public ValueTask<Result<WorkDigestDispatchStatusView>> HandleAsync(
        IRequestContext<AuthorizeWorkDigestUnknownRetry> context, CancellationToken ct)
    {
        var request = context.Request;
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var operatorId))
            return ValueTask.FromResult(Result<WorkDigestDispatchStatusView>.Failure(
                new RequestError(RequestErrorKind.Unauthorized,
                    "Retry authorization requires an authenticated platform operator.")));
        if (request.TenantId == Uuid.Empty || request.MemberId == Uuid.Empty ||
            request.WeekOf.DayOfWeek != DayOfWeek.Monday)
            return ValueTask.FromResult(Result<WorkDigestDispatchStatusView>.Failure(
                new RequestError(RequestErrorKind.Validation,
                    "The work digest retry scope must identify a tenant, member, and local Monday.")));
        if (!IsValidAuditValue(request.EvidenceReference, 256) ||
            !IsValidAuditValue(request.Rationale, 512))
            return ValueTask.FromResult(Result<WorkDigestDispatchStatusView>.Failure(
                new RequestError(RequestErrorKind.Validation,
                    "An evidence reference and rationale are required for retry authorization.")));

        var evidenceReference = request.EvidenceReference.Trim();
        var rationale = request.Rationale.Trim();
        return executor.ExecuteAsync(new WorkDigestDispatch(request.TenantId, request.MemberId),
            dispatch =>
            {
                if (!dispatch.AuthorizeUnknownRetry(request.WeekOf, operatorId,
                        evidenceReference, rationale, clock.GetUtcNow(), settings.MaximumAttempts))
                    return AggregateOutcome.Discard(Result<WorkDigestDispatchStatusView>.Failure(
                        new RequestError(RequestErrorKind.Conflict,
                            "The dispatch is not eligible for a same-week operator-authorized retry.")));

                return AggregateOutcome.Commit(Result<WorkDigestDispatchStatusView>.Success(
                    dispatch.Read(request.WeekOf)));
            }, context, ct);
    }

    static bool IsValidAuditValue(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength &&
        !value.Any(char.IsControl);
}
