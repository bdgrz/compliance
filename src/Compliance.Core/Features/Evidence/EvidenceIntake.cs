using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Artifacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     Captures uploaded content as evidence under M0-D16: store by digest, register as pending inspection, then apply
///     the inspection result. Malware is quarantined in storage; a detected secret or invalid upload is purged and only
///     its tombstone remains. Identical content in a tenant always resolves to one artifact, whose stream serializes
///     concurrent uploads of the same bytes.
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
        if (artifact.IsCreated)
        {
            // A rejected upload's bytes must never reappear, even when uploaded again.
            if (artifact.State == EvidenceArtifactStates.Rejected)
                await store.DeleteAsync(reference, ct).ConfigureAwait(false);
            return Result<EvidenceCapture>.Success(new EvidenceCapture(artifact.Id, artifact.State!,
                artifact.StateReason, true));
        }
        var registered = artifact.Register(normalized, reference.Sha256, reference.Length, collector,
            clock.GetUtcNow());
        if (!registered.IsSuccess)
            return Result<EvidenceCapture>.Failure(registered.Error);
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
                return Result<EvidenceCapture>.Failure(new RequestError(RequestErrorKind.Conflict,
                    failure.Message ?? "The evidence artifact could not record its inspection."));
            if (result == EvidenceInspectionOutcome.Malware)
                await store.QuarantineAsync(reference, ct).ConfigureAwait(false);
            else if (result is EvidenceInspectionOutcome.SecretDetected or EvidenceInspectionOutcome.Invalid)
                await store.DeleteAsync(reference, ct).ConfigureAwait(false);
        }
        await writer.SaveAsync(artifact, dispatch, ct).ConfigureAwait(false);
        return Result<EvidenceCapture>.Success(new EvidenceCapture(artifact.Id, artifact.State!,
            artifact.StateReason, false));
    }

    public static Uuid IdFor(Uuid tenantId, string sha256) =>
        Uuid.CreateVersion5(tenantId, "evidence-content:" + sha256);

    static Result<EvidenceCapture> Validation(string message) =>
        Result<EvidenceCapture>.Failure(new RequestError(RequestErrorKind.Validation, message));
}
