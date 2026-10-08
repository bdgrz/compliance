using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

sealed class EvidenceRedactionRead(EvidenceArtifactReadAccess access, EvidenceRedactionSources sources, IAggregateReader reader)
{
    public async ValueTask<Result<EvidenceRedactionReadSnapshot>> GetAsync(IRequestContext context, Uuid tenantId, Uuid redactionId, CancellationToken ct)
    {
        var allowed = await access.RequireMembershipAsync(context, tenantId, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceRedactionReadSnapshot>.Failure(allowed.Error);
        var redaction = await reader.HydrateAsync(new EvidenceRedaction(tenantId, redactionId), ct).ConfigureAwait(false);
        var position = redaction.CommittedStreamPosition;
        if (redaction.CurrentPreparation is not { } preparation)
            return Result<EvidenceRedactionReadSnapshot>.Failure(new RequestError(RequestErrorKind.NotFound, "The redaction lineage was not found."));
        var captured = await sources.CaptureAsync(context, tenantId, preparation.Original.ArtifactId, preparation.Derived.ArtifactId, ct).ConfigureAwait(false);
        if (!captured.IsSuccess)
            return Result<EvidenceRedactionReadSnapshot>.Failure(captured.Error.Kind == RequestErrorKind.NotFound ?
                new RequestError(RequestErrorKind.NotFound, "The redaction lineage was not found.") : captured.Error);
        if (EvidenceRedactionSources.Identity(captured.Value.Original) != preparation.Original ||
            EvidenceRedactionSources.Identity(captured.Value.Derived) != preparation.Derived)
            return Result<EvidenceRedactionReadSnapshot>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The current artifacts differ from the retained preparation identities."));
        // The current available artifact lifecycle has no later valid mutation. Retained approvals
        // must bind that exact observed source and availability fact, including older preparation approvals.
        if (!HasConsistentApprovalSources(redaction, captured.Value))
            return Result<EvidenceRedactionReadSnapshot>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The retained approval source proof differs from the observed derived artifact."));
        var checkedSources = await sources.CheckAsync(context, captured.Value, ct).ConfigureAwait(false);
        if (!checkedSources.IsSuccess)
            return Result<EvidenceRedactionReadSnapshot>.Failure(checkedSources.Error);
        var current = await reader.HydrateAsync(new EvidenceRedaction(tenantId, redactionId), ct).ConfigureAwait(false);
        if (current.CommittedStreamPosition != position)
            return Result<EvidenceRedactionReadSnapshot>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The redaction lineage changed during this request.", isTransient: true));
        return Result<EvidenceRedactionReadSnapshot>.Success(new(redaction, captured.Value));
    }

    internal static bool HasConsistentApprovalSources(EvidenceRedaction redaction, EvidenceRedactionSourcePair sources) =>
        redaction.Approvals.All(approval => approval.DerivedSourcePosition == sources.Derived.Metadata.SourcePosition &&
            approval.DerivedAvailableAt == sources.Derived.AvailabilityRecordedAt);

    internal static EvidenceRedactionView View(EvidenceRedactionReadSnapshot snapshot) => snapshot.Redaction.PublicView(
        snapshot.Sources.Original.Metadata.State, snapshot.Sources.Derived.Metadata.State,
        snapshot.Redaction.CurrentApproval is not null && snapshot.Sources.Derived.Metadata.State == EvidenceArtifactStates.Available);
}
