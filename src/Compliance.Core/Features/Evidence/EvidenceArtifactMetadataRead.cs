using System.Security.Cryptography;
using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Captures existing canonical metadata without opening, disclosing or copying content.</summary>
sealed class EvidenceArtifactMetadataRead(IAggregateReader reader, IDomainEventReader events)
{
    public async ValueTask<Result<EvidenceArtifactMetadataView>> GetAsync(Uuid tenantId, Uuid artifactId, CancellationToken ct)
    {
        var captured = await GetCapturedAsync(tenantId, artifactId, ct).ConfigureAwait(false);
        return captured.IsSuccess ? Result<EvidenceArtifactMetadataView>.Success(captured.Value.Metadata) :
            Result<EvidenceArtifactMetadataView>.Failure(captured.Error);
    }

    public ValueTask<Result<EvidenceArtifactMetadataSnapshot>> GetCapturedAsync(Uuid tenantId, Uuid artifactId, CancellationToken ct) =>
        CaptureAsync(reader, events, tenantId, artifactId, ct);

    internal static async ValueTask<Result<EvidenceArtifactMetadataSnapshot>> CaptureAsync(IAggregateReader reader,
        IDomainEventReader events, Uuid tenantId, Uuid artifactId, CancellationToken ct)
    {
        var artifact = await reader.HydrateAsync(new EvidenceArtifact(tenantId, artifactId), ct).ConfigureAwait(false);
        if (artifact.Registration is not { } registration || registration.TenantId != tenantId ||
            registration.ArtifactId != artifactId || registration.Metadata.EventId == Uuid.Empty ||
            registration.Metadata.AggregateId != artifactId ||
            registration.Content is not { Title: not null, EvidenceType: not null, Source: not null, HandlingClass: not null } ||
            registration.Collector is null ||
            EvidenceRules.Validate(registration.Content, registration.ContentSha256, registration.ContentLength) is not null)
            return Missing();
        var position = artifact.CommittedStreamPosition;
        ulong expectedOffset = 0;
        string? state = null;
        string? reason = null;
        DateTimeOffset? availabilityRecordedAt = null;
        await foreach (var source in events.ReadAsync(artifact.Stream, 0, ct).WithCancellation(ct).ConfigureAwait(false))
        {
            if (source.Stream != artifact.Stream || source.ResourceOffset != expectedOffset || source.ResourceOffset >= position)
                return Changed();
            if (source.Event.Metadata.EventId == Uuid.Empty || source.Event.Metadata.AggregateId != artifactId)
                return Missing();
            expectedOffset++;
            switch (source.Event)
            {
                case EvidenceArtifactRegistered ev when ev.TenantId == tenantId && ev.ArtifactId == artifactId &&
                                                        state is null && ev == registration &&
                                                        ev.Metadata.EventId == registration.Metadata.EventId:
                    state = EvidenceArtifactStates.PendingInspection;
                    break;
                case EvidenceArtifactInspected ev when ev.TenantId == tenantId && ev.ArtifactId == artifactId &&
                                                       state == EvidenceArtifactStates.PendingInspection && ValidInspection(ev):
                    state = ev.State;
                    reason = ev.Reason;
                    if (ev.State == EvidenceArtifactStates.Available)
                        availabilityRecordedAt = ev.InspectedAt;
                    break;
                case EvidenceArtifactQuarantineReleased ev when ev.TenantId == tenantId && ev.ArtifactId == artifactId &&
                                                                state == EvidenceArtifactStates.Quarantined && reason == EvidenceArtifactStates.Malware &&
                                                                !string.IsNullOrWhiteSpace(ev.Rationale) && ev.Rationale.Length <= EvidenceRules.MaximumTextLength &&
                                                                ev.DecidedBy is not null:
                    state = EvidenceArtifactStates.Available;
                    reason = null;
                    availabilityRecordedAt = ev.DecidedAt;
                    break;
                default:
                    return Missing();
            }
        }
        if (expectedOffset != position)
            return Changed();
        if (state != artifact.State || reason != artifact.StateReason)
            return Missing();
        var view = new EvidenceArtifactMetadataView(tenantId, artifactId, position,
            registration.ContentSha256, registration.ContentLength,
            state == EvidenceArtifactStates.Rejected ? null : registration.Content,
            registration.Collector, registration.RegisteredAt, state!, reason);
        var current = await reader.HydrateAsync(new EvidenceArtifact(tenantId, artifactId), ct).ConfigureAwait(false);
        if (current.CommittedStreamPosition != position)
            return Changed();
        var digest = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(registration,
            ComplianceCoreJsonContext.Default.EvidenceArtifactRegistered)));
        return Result<EvidenceArtifactMetadataSnapshot>.Success(new(view, registration.Metadata.EventId, digest, availabilityRecordedAt));
    }

    public async ValueTask<Result> CheckAsync(EvidenceArtifactMetadataView original, CancellationToken ct)
    {
        var current = await reader.HydrateAsync(new EvidenceArtifact(original.TenantId, original.ArtifactId), ct).ConfigureAwait(false);
        return current.CommittedStreamPosition == original.SourcePosition ? Result.Success : Result.Failure(
            new RequestError(RequestErrorKind.Conflict, "Artifact metadata changed during this request.", isTransient: true));
    }

    static bool ValidInspection(EvidenceArtifactInspected ev) => ev.State switch
    {
        EvidenceArtifactStates.Available => ev.Reason is null,
        EvidenceArtifactStates.Quarantined => ev.Reason == EvidenceArtifactStates.Malware,
        EvidenceArtifactStates.Rejected => ev.Reason is EvidenceArtifactStates.SecretDetected or EvidenceArtifactStates.Invalid,
        _ => false
    };

    static Result<EvidenceArtifactMetadataSnapshot> Missing() => Result<EvidenceArtifactMetadataSnapshot>.Failure(
        new RequestError(RequestErrorKind.NotFound, "The artifact metadata was not found."));
    static Result<EvidenceArtifactMetadataSnapshot> Changed() => Result<EvidenceArtifactMetadataSnapshot>.Failure(
        new RequestError(RequestErrorKind.Conflict, "Artifact metadata changed during this request.", isTransient: true));
}
