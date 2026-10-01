using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     Owns one evidence artifact through the M0-D16 lifecycle. Content stays unavailable until a clean inspection;
///     malware quarantines it until an attributed release, and a detected secret or invalid upload is rejected for
///     good.
/// </summary>
public sealed class EvidenceArtifact : Aggregate
{
    readonly Uuid _tenantId;
    EvidenceArtifactRegistered? _registration;

    public bool IsCreated => _registration is not null;
    public EvidenceArtifactContent? Content => _registration?.Content;
    public string? ContentSha256 => _registration?.ContentSha256;
    public string? State { get; private set; }
    public string? StateReason { get; private set; }

    public EvidenceArtifact(Uuid tenantId, Uuid id)
        : base(id, EvidenceStreams.Address(tenantId, id))
    {
        _tenantId = tenantId;
        On<EvidenceArtifactRegistered>(ev =>
        {
            _registration = ev;
            State = EvidenceArtifactStates.PendingInspection;
        });
        On<EvidenceArtifactInspected>(ev =>
        {
            State = ev.State;
            StateReason = ev.Reason;
        });
        On<EvidenceArtifactQuarantineReleased>(_ =>
        {
            State = EvidenceArtifactStates.Available;
            StateReason = null;
        });
    }

    public Result<EvidenceArtifactRegistration> Register(EvidenceArtifactContent requested, string sha256,
        long length, ActorReference collector, DateTimeOffset registeredAt)
    {
        var content = EvidenceRules.Normalize(requested);
        var error = EvidenceRules.Validate(content, sha256, length);
        if (error is not null)
            return Result<EvidenceArtifactRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                error));
        if (_registration is not null)
            return _registration.Content == content && _registration.ContentSha256 == sha256 &&
                   _registration.ContentLength == length
                ? Result<EvidenceArtifactRegistration>.Success(new EvidenceArtifactRegistration(Id))
                : Result<EvidenceArtifactRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The evidence artifact already exists with different content."));
        RaiseEvent(new EvidenceArtifactRegistered(_tenantId, Id, content, sha256, length, collector,
            registeredAt));
        return Result<EvidenceArtifactRegistration>.Success(new EvidenceArtifactRegistration(Id));
    }

    public CommandFailure? RecordInspection(EvidenceInspectionOutcome outcome, DateTimeOffset inspectedAt)
    {
        if (!IsCreated)
            return CommandFailure.MissingRecord("The evidence artifact was not found.");
        if (State != EvidenceArtifactStates.PendingInspection)
            return CommandFailure.StateConflict("The evidence artifact has already been inspected.");
        var (state, reason) = outcome switch
        {
            EvidenceInspectionOutcome.Clean => (EvidenceArtifactStates.Available, (string?)null),
            EvidenceInspectionOutcome.Malware => (EvidenceArtifactStates.Quarantined, EvidenceArtifactStates.Malware),
            EvidenceInspectionOutcome.SecretDetected =>
                (EvidenceArtifactStates.Rejected, EvidenceArtifactStates.SecretDetected),
            EvidenceInspectionOutcome.Invalid => (EvidenceArtifactStates.Rejected, EvidenceArtifactStates.Invalid),
            _ => (null, null),
        };
        if (state is null)
            return CommandFailure.InvalidContent("The inspection outcome is not recognized.");
        RaiseEvent(new EvidenceArtifactInspected(_tenantId, Id, state, reason, inspectedAt));
        return null;
    }

    // Only a malware quarantine can be released; a detected secret or invalid upload never becomes available.
    public CommandFailure? ReleaseQuarantine(string rationale, ActorReference decidedBy, DateTimeOffset decidedAt)
    {
        if (!IsCreated)
            return CommandFailure.MissingRecord("The evidence artifact was not found.");
        if (State != EvidenceArtifactStates.Quarantined)
            return CommandFailure.StateConflict("Only a quarantined evidence artifact can be released.");
        if (string.IsNullOrWhiteSpace(rationale) || rationale.Trim().Length > EvidenceRules.MaximumTextLength)
            return CommandFailure.InvalidContent("A quarantine release requires a rationale.");
        RaiseEvent(new EvidenceArtifactQuarantineReleased(_tenantId, Id, rationale.Trim(), decidedBy, decidedAt));
        return null;
    }
}
