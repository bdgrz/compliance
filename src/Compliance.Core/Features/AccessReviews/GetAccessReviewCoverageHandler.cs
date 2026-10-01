using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Reconciles every instance of an application to its effective scope and either an accepted
///     population observed by <c>AsOf</c> or an approved exception. Every projection it reads must
///     have reached its source, so a missing population never reflects lag.
/// </summary>
public sealed class GetAccessReviewCoverageHandler(IApplicationDirectoryReader applications,
    SystemInstanceReadConsistency consistency, IAccessReviewScopeDirectoryReader scopes,
    IAccessPopulationDirectoryReader populations, IAggregateReader reader,
    IDomainEventReader events, TimeProvider clock)
    : IRequestHandler<GetAccessReviewCoverage, AccessReviewCoverageView>
{
    const int MaximumInstances = 1000;

    public async ValueTask<Result<AccessReviewCoverageView>> HandleAsync(
        IRequestContext<GetAccessReviewCoverage> context, CancellationToken ct)
    {
        var request = context.Request;
        var fresh = await consistency.EnsureAsync(request.TenantId, request.ApplicationId, null,
            null, null, ct).ConfigureAwait(false);
        if (!fresh.IsSuccess)
            return Result<AccessReviewCoverageView>.Failure(fresh.Error);
        if (!await AccessReviewOutcome.IsCaughtUpAsync(events,
                AccessReviewScopeStreams.TenantPattern(request.TenantId),
                await scopes.LoadCheckpointAsync(request.TenantId, ct).ConfigureAwait(false), ct)
                .ConfigureAwait(false) ||
            !await AccessReviewOutcome.IsCaughtUpAsync(events,
                AccessReviewDirectorySchema.PopulationPattern(request.TenantId),
                await populations.LoadCheckpointAsync(request.TenantId, ct).ConfigureAwait(false), ct)
                .ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessReviewCoverageView>(RequestErrorKind.Conflict,
                "The access-review projections have not reached the source.", true);

        var asOf = request.AsOf ?? clock.GetUtcNow();
        var coverage = new List<AccessReviewInstanceCoverageView>();
        string? cursor = null;
        do
        {
            var page = await applications.ListInstancesAsync(request.TenantId,
                request.ApplicationId, 200, cursor, ct).ConfigureAwait(false);
            foreach (var instance in page.Items)
            {
                if (instance.TenantId != request.TenantId ||
                    instance.ApplicationId != request.ApplicationId)
                    return AccessReviewOutcome.Failure<AccessReviewCoverageView>(
                        RequestErrorKind.NotFound, "The system instances were not found.");
                coverage.Add(await EvaluateAsync(request.TenantId, instance, asOf, ct)
                    .ConfigureAwait(false));
            }
            cursor = page.NextCursor;
        } while (cursor is not null && coverage.Count < MaximumInstances);
        return Result<AccessReviewCoverageView>.Success(new AccessReviewCoverageView(
            request.TenantId, request.ApplicationId, asOf, coverage));
    }

    async ValueTask<AccessReviewInstanceCoverageView> EvaluateAsync(Uuid tenantId,
        SystemInstanceView instance, DateTimeOffset asOf, CancellationToken ct)
    {
        var record = await scopes.GetAsync(tenantId, instance.SystemInstanceId, ct).ConfigureAwait(false);
        var scope = AccessReviewScopeStatus.Evaluate(instance, record?.Decisions ?? [], asOf).Status;
        if (scope != SystemInstanceAccessReviewScope.Included)
            return new AccessReviewInstanceCoverageView(instance.SystemInstanceId, instance.Name,
                scope, scope == SystemInstanceAccessReviewScope.Excluded ? "not_in_scope" : "scope_unresolved",
                null, null, null, null);
        AccessPopulationSummaryView? accepted = null;
        string? cursor = null;
        do
        {
            var page = await populations.ListAsync(tenantId, instance.SystemInstanceId, 200, cursor, ct)
                .ConfigureAwait(false);
            accepted = page.Items.FirstOrDefault(population =>
                population.Status == AccessPopulation.Accepted && population.ObservedAt <= asOf &&
                population.AcceptedAt <= asOf);
            cursor = accepted is null ? page.NextCursor : null;
        } while (cursor is not null);
        if (accepted is not null)
            return new AccessReviewInstanceCoverageView(instance.SystemInstanceId, instance.Name,
                scope, "covered", accepted.PopulationId, accepted.SnapshotId, accepted.ObservedAt, null);
        var ledger = await reader.HydrateAsync(new AccessReviewSystemLedger(tenantId,
            instance.SystemInstanceId), ct).ConfigureAwait(false);
        var exception = ledger.PopulationExceptionAt(asOf);
        return new AccessReviewInstanceCoverageView(instance.SystemInstanceId, instance.Name, scope,
            exception is null ? "missing_population" : "excepted", null, null, null, exception);
    }
}
