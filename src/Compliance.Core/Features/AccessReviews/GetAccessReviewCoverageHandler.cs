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
    IDomainEventReader events, TimeProvider clock,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<GetAccessReviewCoverage, AccessReviewCoverageView>
{
    public async ValueTask<Result<AccessReviewCoverageView>> HandleAsync(
        IRequestContext<GetAccessReviewCoverage> context, CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (!await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.ApplicationId, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessReviewCoverageView>(RequestErrorKind.NotFound,
                "The application was not found.");
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
                var evaluated = await EvaluateAsync(request.TenantId, instance, asOf, ct)
                    .ConfigureAwait(false);
                if (!evaluated.IsSuccess)
                    return Result<AccessReviewCoverageView>.Failure(evaluated.Error);
                coverage.Add(evaluated.Value);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
        return Result<AccessReviewCoverageView>.Success(new AccessReviewCoverageView(
            request.TenantId, request.ApplicationId, asOf, coverage));
    }

    async ValueTask<Result<AccessReviewInstanceCoverageView>> EvaluateAsync(Uuid tenantId,
        SystemInstanceView instance, DateTimeOffset asOf, CancellationToken ct)
    {
        var record = await scopes.GetAsync(tenantId, instance.SystemInstanceId, ct).ConfigureAwait(false);
        if (record is not null && (record.TenantId != tenantId ||
                                   record.ApplicationId != instance.ApplicationId ||
                                   record.SystemInstanceId != instance.SystemInstanceId ||
                                   record.Decisions is null ||
                                   record.Decisions.Any(decision => decision.TenantId != tenantId ||
                                       decision.ApplicationId != instance.ApplicationId ||
                                       decision.SystemInstanceId != instance.SystemInstanceId)))
            return AccessReviewOutcome.Failure<AccessReviewInstanceCoverageView>(
                RequestErrorKind.NotFound, "The system instances were not found.");
        var scope = AccessReviewScopeStatus.Evaluate(instance, record?.Decisions ?? [], asOf).Status;
        if (scope != SystemInstanceAccessReviewScope.Included)
            return Result<AccessReviewInstanceCoverageView>.Success(
                new AccessReviewInstanceCoverageView(instance.SystemInstanceId, instance.Name,
                    scope, scope == SystemInstanceAccessReviewScope.Excluded
                        ? "not_in_scope"
                        : "scope_unresolved", null, null, null, null));
        AccessPopulationSummaryView? accepted = null;
        string? cursor = null;
        do
        {
            var page = await populations.ListAsync(tenantId, instance.SystemInstanceId, 200, cursor, ct)
                .ConfigureAwait(false);
            if (page.Items.Any(population => population.TenantId != tenantId ||
                                             population.ApplicationId != instance.ApplicationId ||
                                             population.SystemInstanceId != instance.SystemInstanceId))
                return AccessReviewOutcome.Failure<AccessReviewInstanceCoverageView>(
                    RequestErrorKind.NotFound, "The populations were not found.");
            accepted = page.Items.FirstOrDefault(population => IsAcceptedSnapshotAt(population, asOf));
            cursor = accepted is null ? page.NextCursor : null;
        } while (cursor is not null);
        if (accepted is not null)
            return Result<AccessReviewInstanceCoverageView>.Success(
                new AccessReviewInstanceCoverageView(instance.SystemInstanceId, instance.Name,
                    scope, "covered", accepted.PopulationId, accepted.SnapshotId,
                    accepted.ObservedAt, null));
        var ledger = await reader.HydrateAsync(new AccessReviewSystemLedger(tenantId,
            instance.SystemInstanceId), ct).ConfigureAwait(false);
        var exception = ledger.PopulationExceptionAt(asOf);
        return Result<AccessReviewInstanceCoverageView>.Success(
            new AccessReviewInstanceCoverageView(instance.SystemInstanceId, instance.Name, scope,
                exception is null ? "missing_population" : "excepted", null, null, null, exception));
    }

    static bool IsAcceptedSnapshotAt(AccessPopulationSummaryView population, DateTimeOffset asOf) =>
        population.Status == AccessPopulation.Accepted && population.ObservedAt <= asOf &&
        population.AcceptedAt is { } acceptedAt && acceptedAt <= asOf &&
        population.SnapshotId is { } snapshotId && snapshotId != Uuid.Empty &&
        population.ContentSha256 is { Length: 64 } contentSha256 &&
        contentSha256.All(Uri.IsHexDigit);
}
