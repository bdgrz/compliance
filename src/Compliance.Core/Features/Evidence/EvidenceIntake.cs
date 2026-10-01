using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     Captures uploaded content as evidence under M0-D16: store by digest, register as pending inspection, then apply
///     the inspection result durably before its storage effect. Request retries resume pending inspection or the
///     recorded verdict's storage effect. Malware is quarantined; a detected secret or invalid upload is purged.
///     Identical content in a tenant resolves to one artifact, preserving its initial metadata and collector.
/// </summary>
public sealed class EvidenceIntake(IArtifactContentStore store, IArtifactInspector inspector,
    IAggregateReader reader, IAggregateWriter writer, TimeProvider clock)
{
    public async ValueTask<Result<EvidenceCapture>> CaptureAsync(Uuid tenantId, Stream content,
        EvidenceArtifactContent metadata, ActorReference collector, RequestDispatchContext dispatch,
        CancellationToken ct)
    {
        var normalized = EvidenceRules.Normalize(metadata);
        if (EvidenceRules.ValidateMetadata(normalized) is { } invalid)
            return Validation(invalid);
        ArtifactContentWrite written;
        try
        {
            written = await store.StoreIfAbsentAsync(tenantId, content, ct).ConfigureAwait(false);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Validation($"Evidence content may not exceed the maximum artifact size.");
        }
        var reference = written.Content;
        var artifact = await reader.HydrateAsync(new EvidenceArtifact(tenantId, IdFor(tenantId, reference.Sha256)),
            ct).ConfigureAwait(false);
        var duplicate = artifact.IsCreated;
        if (!duplicate)
        {
            var registered = artifact.Register(normalized, reference.Sha256, reference.Length, collector,
                clock.GetUtcNow());
            if (!registered.IsSuccess)
                return Result<EvidenceCapture>.Failure(registered.Error);
            await writer.SaveAsync(artifact, dispatch, ct).ConfigureAwait(false);
        }
        if (artifact.State == EvidenceArtifactStates.PendingInspection)
        {
            var inspection = await inspector.InspectAsync(reference, ct).ConfigureAwait(false);
            var outcome = inspection.State switch
            {
                ArtifactInspectionState.Clean => EvidenceInspectionOutcome.Clean,
                ArtifactInspectionState.Quarantined => EvidenceInspectionOutcome.Malware,
                ArtifactInspectionState.SecretDetected => EvidenceInspectionOutcome.SecretDetected,
                ArtifactInspectionState.Invalid => EvidenceInspectionOutcome.Invalid,
                _ => (EvidenceInspectionOutcome?)null,
            };
            if (outcome is { } result)
            {
                if (artifact.RecordInspection(result, clock.GetUtcNow()) is { } failure)
                    return Conflict(failure.Message ?? "The evidence artifact could not record its inspection.");
                await writer.SaveAsync(artifact, dispatch, ct).ConfigureAwait(false);
            }
        }
        if (artifact.State == EvidenceArtifactStates.Quarantined &&
            !await store.QuarantineAsync(reference, ct).ConfigureAwait(false))
        {
            await using var quarantined = await store.OpenQuarantinedAsync(reference, ct).ConfigureAwait(false);
            if (quarantined is null)
                return Conflict("The evidence content could not be quarantined.");
        }
        else if (artifact.State == EvidenceArtifactStates.Rejected &&
                 !await PurgeAsync(reference, ct).ConfigureAwait(false))
            return Conflict("The rejected evidence content could not be purged.");
        return Result<EvidenceCapture>.Success(new EvidenceCapture(artifact.Id, artifact.State!,
            artifact.StateReason, duplicate));
    }

    public static Uuid IdFor(Uuid tenantId, string sha256) =>
        Uuid.CreateVersion5(tenantId, "evidence-content:" + sha256);

    async ValueTask<bool> PurgeAsync(ArtifactContentReference content, CancellationToken ct) =>
        await store.DeleteAsync(content, ct).ConfigureAwait(false) is
            ArtifactDeletionResult.Deleted or ArtifactDeletionResult.NotFound;

    static Result<EvidenceCapture> Conflict(string message) =>
        Result<EvidenceCapture>.Failure(new RequestError(RequestErrorKind.Conflict, message));

    static Result<EvidenceCapture> Validation(string message) =>
        Result<EvidenceCapture>.Failure(new RequestError(RequestErrorKind.Validation, message));
}
