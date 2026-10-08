using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

sealed class EvidenceRedactionSources(EvidenceArtifactReadAccess access, EvidenceArtifactMetadataRead read)
{
    public async ValueTask<Result<EvidenceRedactionSourcePair>> CaptureAsync(IRequestContext context, Uuid tenantId,
        Uuid originalId, Uuid derivedId, CancellationToken ct)
    {
        foreach (var id in new[] { originalId, derivedId })
        {
            var allowed = await access.RequireAsync(context, tenantId, id, ct).ConfigureAwait(false);
            if (!allowed.IsSuccess)
                return Result<EvidenceRedactionSourcePair>.Failure(allowed.Error);
        }
        var original = await read.GetCapturedAsync(tenantId, originalId, ct).ConfigureAwait(false);
        if (!original.IsSuccess)
            return Result<EvidenceRedactionSourcePair>.Failure(original.Error);
        var derived = await read.GetCapturedAsync(tenantId, derivedId, ct).ConfigureAwait(false);
        return derived.IsSuccess ? Result<EvidenceRedactionSourcePair>.Success(new(original.Value, derived.Value)) :
            Result<EvidenceRedactionSourcePair>.Failure(derived.Error);
    }

    public async ValueTask<Result> CheckAsync(IRequestContext context, EvidenceRedactionSourcePair pair, CancellationToken ct)
    {
        foreach (var snapshot in new[] { pair.Original, pair.Derived })
        {
            var metadata = snapshot.Metadata;
            var allowed = await access.RequireAsync(context, metadata.TenantId, metadata.ArtifactId, ct).ConfigureAwait(false);
            if (!allowed.IsSuccess)
                return allowed;
            var current = await read.CheckAsync(metadata, ct).ConfigureAwait(false);
            if (!current.IsSuccess)
                return current;
        }
        return Result.Success;
    }

    internal static EvidenceRedactionSourceCapsule Identity(EvidenceArtifactMetadataSnapshot source)
    {
        var view = source.Metadata;
        var identity = new EvidenceRedactionSourceCapsule(view.TenantId, view.ArtifactId, source.RegistrationEventId,
            source.RegistrationPayloadSha256, view.ContentSha256, view.ContentLength, view.RegisteredAt, "");
        return identity with { IdentitySha256 = EvidenceRedaction.IdentityDigest(identity) };
    }
}
