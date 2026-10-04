using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Explains an accepted population with the expectations effective when it was observed.</summary>
public sealed class GetAccessVarianceHandler(IAggregateReader reader,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<GetAccessVariance, AccessVarianceView>
{
    public async ValueTask<Result<AccessVarianceView>> HandleAsync(
        IRequestContext<GetAccessVariance> context, CancellationToken ct)
    {
        var request = context.Request;
        var population = await reader.HydrateAsync(new AccessPopulation(request.TenantId,
            request.PopulationId), ct).ConfigureAwait(false);
        if (population.Opened is not { } opened)
            return AccessReviewOutcome.Failure<AccessVarianceView>(RequestErrorKind.NotFound,
                "The population was not found.");
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, userId,
                opened.ApplicationId, opened.SystemInstanceId, ct)
            .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessVarianceView>(RequestErrorKind.NotFound,
                "The population was not found.");
        var loaded = await AcceptedAccessPopulation.LoadAsync(reader, request.TenantId,
            request.PopulationId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<AccessVarianceView>.Failure(loaded.Error);
        var accepted = loaded.Value;
        var items = await EvaluateAsync(reader, request.TenantId, accepted, ct).ConfigureAwait(false);
        return Result<AccessVarianceView>.Success(new AccessVarianceView(request.TenantId,
            accepted.Population.Id, accepted.Snapshot.Id, accepted.CalculationId,
            accepted.Header.ObservedAt, items));
    }

    internal static async ValueTask<IReadOnlyList<AccessVarianceItemView>> EvaluateAsync(
        IAggregateReader reader, Uuid tenantId, AcceptedAccessPopulation accepted,
        CancellationToken ct)
    {
        var ledger = await reader.HydrateAsync(new AccessReviewSystemLedger(tenantId,
            accepted.Header.SystemInstanceId), ct).ConfigureAwait(false);
        var observedAt = accepted.Header.ObservedAt;
        return AccessVarianceEvaluator.Evaluate(accepted.Facts, accepted.EffectiveAccess,
            accepted.ClassificationOf, ledger.EffectiveAt(observedAt), ledger.Exceptions, observedAt);
    }
}
