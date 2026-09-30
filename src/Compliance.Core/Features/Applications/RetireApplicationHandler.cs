using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Retires an application, optionally as merged into an active successor.</summary>
public sealed class RetireApplicationHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<RetireApplication>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RetireApplication> context,
        CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        if (request.MergedIntoApplicationId is { } successorId &&
            successorId != request.ApplicationId && successorId != Uuid.Empty)
        {
            var successor = await reader.HydrateAsync(new DeclaredApplication(request.TenantId,
                successorId), ct).ConfigureAwait(false);
            if (!successor.IsCreated)
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "The merged-into application was not found."));
            if (successor.IsRetired)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The merged-into application is retired."));
        }
        return await executor.ExecuteAsync(new DeclaredApplication(request.TenantId,
                request.ApplicationId),
            app => CommandFailureRequestAdapter.ToOutcome(app.Retire(request.ExpectedRevision,
                request.EffectiveAt, request.Reason, request.MergedIntoApplicationId,
                memberId, display, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
