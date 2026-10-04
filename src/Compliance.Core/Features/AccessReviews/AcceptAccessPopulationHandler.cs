using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Freezes the validated draft through the population snapshot primitive, then records the
///     attestation. A retried request replays both steps instead of freezing twice.
/// </summary>
public sealed class AcceptAccessPopulationHandler(IAggregateReader reader,
    IAggregateExecutor executor, PopulationSnapshotFreezer freezer, TimeProvider clock,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<AcceptAccessPopulation, AccessPopulationAcceptance>
{
    public async ValueTask<Result<AccessPopulationAcceptance>> HandleAsync(
        IRequestContext<AcceptAccessPopulation> context, CancellationToken ct)
    {
        var request = context.Request;
        var population = await reader.HydrateAsync(new AccessPopulation(request.TenantId,
            request.PopulationId), ct).ConfigureAwait(false);
        if (population.Opened is not { } opened)
            return AccessReviewOutcome.Failure<AccessPopulationAcceptance>(RequestErrorKind.NotFound,
                "The population was not found.");
        var actor = AccessReviewActor.From(context);
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, actor.UserId,
                opened.ApplicationId, opened.SystemInstanceId, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessPopulationAcceptance>(RequestErrorKind.NotFound,
                "The population was not found.");
        if (population.Acceptance is { } accepted)
            return accepted.SnapshotId == context.RequestId
                ? Result<AccessPopulationAcceptance>.Success(new(population.Id, accepted.SnapshotId,
                    accepted.ContentSha256, accepted.CalculationId))
                : AccessReviewOutcome.Failure<AccessPopulationAcceptance>(RequestErrorKind.Conflict,
                    "The population was already accepted.");
        if (population.Revision != request.ExpectedRevision)
            return AccessReviewOutcome.Failure<AccessPopulationAcceptance>(RequestErrorKind.Conflict,
                $"The population is at revision {population.Revision}; expected {request.ExpectedRevision}.");
        if (AccessPopulationAnalysis.Validate(population.Facts).Count > 0)
            return AccessReviewOutcome.Failure<AccessPopulationAcceptance>(RequestErrorKind.Validation,
                "Resolve every population issue before acceptance.");
        if (!AccessReviewVocabulary.IsBoundedText(request.Attestation, 4000))
            return AccessReviewOutcome.Failure<AccessPopulationAcceptance>(RequestErrorKind.Validation,
                "Acceptance requires an attestation of at most 4000 characters.");

        var header = new AccessPopulationHeader(population.Id, opened.ApplicationId,
            opened.SystemInstanceId, opened.SystemInstanceRevision, opened.ObservedAt,
            opened.SourceKind, opened.Source, request.Attestation.Trim());
        var frozen = await freezer.FreezeAsync(context, request.TenantId, AccessPopulationContent.Kind,
            AccessPopulationContent.Rows(header, population.Facts), null, null, actor.Reference, ct)
            .ConfigureAwait(false);
        if (!frozen.IsSuccess)
            return Result<AccessPopulationAcceptance>.Failure(frozen.Error);
        return await executor.ExecuteAsync(new AccessPopulation(request.TenantId, request.PopulationId),
            draft => AccessReviewOutcome.From(draft.Accept(request.ExpectedRevision,
                frozen.Value.SnapshotId, frozen.Value.ContentSha256,
                AccessPopulationAnalysis.CalculationId(frozen.Value.ContentSha256),
                request.Attestation, actor.Reference, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
