using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Commitments;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Inspects the authoritative provider and governed artifacts an assurance record cites.</summary>
public sealed class AssuranceReferences(IAggregateReader reader, IAssuranceReader assurance,
    AssuranceReadConsistency assuranceConsistency,
    CommitmentVersionReadConsistency commitmentConsistency)
{
    public AssuranceReferences(IAggregateReader reader)
        : this(reader, null!, null!, null!)
    {
    }

    public async ValueTask<Result<ProviderView>> ProviderAsync(Uuid tenantId, Uuid providerId, CancellationToken ct)
    {
        var register = await reader.HydrateAsync(new ProviderRegister(tenantId), ct).ConfigureAwait(false);
        return register.Get(providerId) is { } provider && provider.TenantId == tenantId
            ? Result<ProviderView>.Success(provider)
            : Result<ProviderView>.Failure(new RequestError(RequestErrorKind.NotFound, "The provider was not found."));
    }

    public async ValueTask<Result> ArtifactAsync(Uuid tenantId, ProviderSourceCitation? citation,
        CancellationToken ct) =>
        await ArtifactByIdAsync(tenantId, citation?.ArtifactId, ct).ConfigureAwait(false);

    public async ValueTask<Result> ArtifactByIdAsync(Uuid tenantId, Uuid? artifactId,
        CancellationToken ct)
    {
        if (artifactId is not { } id ||
            (await reader.HydrateAsync(new EvidenceArtifact(tenantId, id), ct).ConfigureAwait(false)).IsCreated)
            return Result.Success;
        return Result.Failure(new RequestError(RequestErrorKind.NotFound,
            "A governed artifact reference was not found in its owning context."));
    }

    public static RequestError? ServiceError(ProviderView provider, Uuid serviceId,
        Uuid? programId = null)
    {
        var declared = (provider.Content.Dependencies ?? []).Any(dependency =>
            dependency.SubjectKind == "client_service" && dependency.SubjectId == serviceId &&
            (programId is null || dependency.ProgramId == programId));
        return declared
            ? null
            : new RequestError(RequestErrorKind.Validation,
                "The provider does not declare the service named by this coverage gap.");
    }

    public async ValueTask<Result> CoverageGapSourceAsync(Uuid tenantId, ProviderView provider,
        Uuid serviceId, string sourceKind, Uuid sourceId, long sourceRevision,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(assurance);
        ArgumentNullException.ThrowIfNull(assuranceConsistency);
        ArgumentNullException.ThrowIfNull(commitmentConsistency);
        if (ServiceError(provider, serviceId) is { } serviceError)
            return Result.Failure(serviceError);
        if (sourceRevision < 1 || sourceId == Uuid.Empty)
            return SourceNotFound();

        switch (sourceKind)
        {
            case "assurance_report":
                {
                    var fence = await assuranceConsistency.CaptureFenceAsync(tenantId, ct)
                        .ConfigureAwait(false);
                    if (!fence.IsSuccess)
                        return Result.Failure(fence.Error);
                    var report = await assurance.GetReportRevisionAsync(tenantId, sourceId,
                        sourceRevision, ct).ConfigureAwait(false);
                    var unchanged = await assuranceConsistency.ConfirmUnchangedAndCaughtUpAsync(
                        tenantId, fence.Value, ct).ConfigureAwait(false);
                    if (!unchanged.IsSuccess)
                        return unchanged;
                    return report is { } retained && retained.TenantId == tenantId &&
                           retained.ProviderId == provider.ProviderId
                        ? Result.Success
                        : SourceNotFound();
                }
            case "provider_review":
                {
                    if (sourceRevision != 1)
                        return SourceNotFound();
                    var register = await reader.HydrateAsync(new ProviderAssuranceRegister(tenantId), ct)
                        .ConfigureAwait(false);
                    return register.Reviews(provider.ProviderId).Any(review =>
                        review.TenantId == tenantId && review.ReviewId == sourceId)
                        ? Result.Success
                        : SourceNotFound();
                }
            case "service_commitment" or "system_requirement" or "subservice_responsibility":
                {
                    var draft = await reader.HydrateAsync(new CommitmentDraft(tenantId, sourceId), ct)
                        .ConfigureAwait(false);
                    if (!draft.IsCreated)
                        return SourceNotFound();
                    var version = await commitmentConsistency.GetAsync(tenantId, draft.ProgramId,
                        sourceId, sourceRevision, ct).ConfigureAwait(false);
                    return version.IsSuccess && version.Value.Kind == sourceKind &&
                           version.Value.ServiceId == serviceId &&
                           (sourceKind != "subservice_responsibility" ||
                            version.Value.ProviderId == provider.ProviderId)
                        ? Result.Success
                        : version.IsSuccess ? SourceNotFound() : Result.Failure(version.Error);
                }
            case "evidence_artifact":
                if (sourceRevision != 1)
                    return SourceNotFound();
                var artifact = await reader.HydrateAsync(new EvidenceArtifact(tenantId, sourceId), ct)
                    .ConfigureAwait(false);
                if (!artifact.IsCreated)
                    return SourceNotFound();
                return artifact.State == EvidenceArtifactStates.Available
                    ? Result.Success
                    : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The provider coverage evidence artifact is not available for use."));
            default:
                return SourceNotFound();
        }
    }

    static Result SourceNotFound() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "The provider coverage source was not found in its owning context."));
}
