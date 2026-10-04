using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Reads a draft from its stream, or an accepted population from its verified snapshot.</summary>
public sealed class GetAccessPopulationHandler(IAggregateReader reader,
    RestrictedApplicationVisibility visibility)
    : IRequestHandler<GetAccessPopulation, AccessPopulationView>
{
    public async ValueTask<Result<AccessPopulationView>> HandleAsync(
        IRequestContext<GetAccessPopulation> context, CancellationToken ct)
    {
        var request = context.Request;
        var population = await reader.HydrateAsync(new AccessPopulation(request.TenantId,
            request.PopulationId), ct).ConfigureAwait(false);
        if (population.Opened is not { } opened)
            return AccessReviewOutcome.Failure<AccessPopulationView>(RequestErrorKind.NotFound,
                "The population was not found.");
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (!await visibility.CanReadSystemInstanceAsync(request.TenantId, userId,
                opened.ApplicationId, opened.SystemInstanceId, ct).ConfigureAwait(false))
            return AccessReviewOutcome.Failure<AccessPopulationView>(RequestErrorKind.NotFound,
                "The population was not found.");
        var facts = population.Facts;
        IReadOnlyList<AccessPopulationIssue> issues;
        IReadOnlyList<EffectiveAccessView> effective;
        string? attestation = null;
        if (population.IsAccepted)
        {
            var accepted = await AcceptedAccessPopulation.LoadAsync(reader, request.TenantId,
                request.PopulationId, ct).ConfigureAwait(false);
            if (!accepted.IsSuccess)
                return Result<AccessPopulationView>.Failure(accepted.Error);
            facts = accepted.Value.Facts;
            effective = accepted.Value.EffectiveAccess;
            attestation = accepted.Value.Header.Attestation;
            issues = [];
        }
        else
        {
            issues = AccessPopulationAnalysis.Validate(facts);
            effective = AccessPopulationAnalysis.Derive(facts);
        }
        var acceptance = population.Acceptance;
        return Result<AccessPopulationView>.Success(new AccessPopulationView(request.TenantId,
            population.Id, population.Revision, opened.ApplicationId, opened.SystemInstanceId,
            opened.SystemInstanceRevision, opened.ObservedAt, opened.SourceKind, opened.Source,
            population.Status, facts, issues, effective, acceptance?.CalculationId,
            acceptance?.SnapshotId, acceptance?.ContentSha256, attestation, opened.OpenedBy,
            acceptance?.AcceptedBy, acceptance?.AcceptedAt));
    }
}
