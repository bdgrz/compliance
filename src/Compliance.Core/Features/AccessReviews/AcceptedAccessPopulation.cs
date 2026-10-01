using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     An accepted population read back from its immutable snapshot, with its derived effective
///     access and current classifications.
/// </summary>
public sealed record AcceptedAccessPopulation(AccessPopulation Population,
    PopulationSnapshot Snapshot, AccessPopulationHeader Header, AccessPopulationFacts Facts,
    IReadOnlyList<EffectiveAccessView> EffectiveAccess)
{
    public Uuid CalculationId => Population.Acceptance!.CalculationId;

    /// <summary>The current explicit classification, or <c>unclassified</c> when none was recorded.</summary>
    public string ClassificationOf(string providerSubjectId) =>
        Population.CurrentClassification(providerSubjectId)?.Classification ??
        AccessReviewVocabulary.Unclassified;

    public static async ValueTask<Result<AcceptedAccessPopulation>> LoadAsync(
        IAggregateReader reader, Uuid tenantId, Uuid populationId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var population = await reader.HydrateAsync(new AccessPopulation(tenantId, populationId), ct)
            .ConfigureAwait(false);
        if (!population.IsOpened)
            return Failure(RequestErrorKind.NotFound, "The population was not found.");
        if (population.Acceptance is not { } accepted)
            return Failure(RequestErrorKind.Conflict, "The population has not been accepted.");
        var snapshot = await reader.HydrateAsync(new PopulationSnapshot(tenantId, accepted.SnapshotId),
            ct).ConfigureAwait(false);
        if (!snapshot.IsFrozen || snapshot.Kind != AccessPopulationContent.Kind ||
            !snapshot.HasIntactContent || snapshot.ContentSha256 != accepted.ContentSha256)
            return Failure(RequestErrorKind.Conflict,
                "The accepted population snapshot failed integrity verification.");
        var (header, facts) = AccessPopulationContent.Read(snapshot);
        if (header.PopulationId != populationId)
            return Failure(RequestErrorKind.Conflict,
                "The accepted population snapshot belongs to another population.");
        return Result<AcceptedAccessPopulation>.Success(new AcceptedAccessPopulation(population,
            snapshot, header, facts, AccessPopulationAnalysis.Derive(facts)));
    }

    static Result<AcceptedAccessPopulation> Failure(RequestErrorKind kind, string message) =>
        Result<AcceptedAccessPopulation>.Failure(new RequestError(kind, message));
}
