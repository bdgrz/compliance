using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Inspects the authoritative provider and governed artifacts an assurance record cites.</summary>
public sealed class AssuranceReferences(IAggregateReader reader)
{
    public async ValueTask<Result<ProviderView>> ProviderAsync(Uuid tenantId, Uuid providerId, CancellationToken ct)
    {
        var register = await reader.HydrateAsync(new ProviderRegister(tenantId), ct).ConfigureAwait(false);
        return register.Get(providerId) is { } provider && provider.TenantId == tenantId
            ? Result<ProviderView>.Success(provider)
            : Result<ProviderView>.Failure(new RequestError(RequestErrorKind.NotFound, "The provider was not found."));
    }

    public async ValueTask<Result> ArtifactAsync(Uuid tenantId, ProviderSourceCitation? citation, CancellationToken ct)
    {
        if (citation?.ArtifactId is not { } artifactId ||
            (await reader.HydrateAsync(new EvidenceArtifact(tenantId, artifactId), ct).ConfigureAwait(false)).IsCreated)
            return Result.Success;
        return Result.Failure(new RequestError(RequestErrorKind.NotFound,
            "A governed artifact reference was not found in its owning context."));
    }
}
