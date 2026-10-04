using Bdgrz.Compliance.Features.Artifacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     Finishes a durable inspection verdict using the current canonical registration. Source,
///     content and checkpoints are separate resources; a later source transition can race an effect.
/// </summary>
public sealed class EvidenceArtifactStorageReconciler(IArtifactContentStore store, IAggregateReader reader)
{
    public async ValueTask<Result<EvidenceArtifact>> ReconcileAsync(Uuid tenantId, Uuid artifactId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var artifact = await reader.HydrateAsync(new EvidenceArtifact(tenantId, artifactId), ct)
            .ConfigureAwait(false);
        var registration = artifact.Registration;
        if (tenantId == Uuid.Empty || artifactId == Uuid.Empty || registration is null ||
            registration.TenantId != tenantId || registration.ArtifactId != artifactId ||
            registration.Content is null ||
            EvidenceRules.Validate(registration.Content, registration.ContentSha256, registration.ContentLength) is not null)
            return Failure("The evidence artifact has no valid canonical content registration.");
        var reference = new ArtifactContentReference(registration.TenantId,
            registration.ContentSha256, registration.ContentLength);
        ct.ThrowIfCancellationRequested();
        switch (artifact.State)
        {
            case EvidenceArtifactStates.Quarantined when artifact.StateReason == EvidenceArtifactStates.Malware:
                if (!await store.QuarantineAsync(reference, ct).ConfigureAwait(false))
                {
                    await using var verified = await store.OpenQuarantinedAsync(reference, ct).ConfigureAwait(false);
                    if (verified is null)
                        return Failure("The evidence content could not be quarantined.");
                }
                break;
            case EvidenceArtifactStates.Rejected when artifact.StateReason is
                EvidenceArtifactStates.SecretDetected or EvidenceArtifactStates.Invalid:
                if (await store.DeleteAsync(reference, ct).ConfigureAwait(false) is not
                    (ArtifactDeletionResult.Deleted or ArtifactDeletionResult.NotFound))
                    return Failure("The rejected evidence content could not be purged.");
                break;
            case EvidenceArtifactStates.PendingInspection:
            case EvidenceArtifactStates.Available:
            case EvidenceArtifactStates.Disposed:
                break;
            default:
                return Failure("The evidence artifact has an unrecognized inspection state or reason.");
        }
        ct.ThrowIfCancellationRequested();
        return Result<EvidenceArtifact>.Success(artifact);
    }

    static Result<EvidenceArtifact> Failure(string message) =>
        Result<EvidenceArtifact>.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
