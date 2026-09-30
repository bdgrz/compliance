using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed class CommitmentDraft : Aggregate
{
    // Fitz stream frames are u16; leave room for the Portia envelope and metadata.
    public const int MaximumDraftEventPayloadBytes = 48 * 1024;
    static readonly HashSet<string> AllowedKinds = new(StringComparer.Ordinal)
    {
        "service_commitment", "system_requirement", "user_entity_responsibility",
        "subservice_responsibility",
    };

    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    Uuid _createRequestId;
    string? _initialStatement;
    string? _initialContext;
    string? _initialSourceReference;
    readonly HashSet<Uuid> _pendingAuthors = [];
    readonly List<(long Version, long Revision, DateOnly EffectiveFrom)> _versions = [];

    public bool IsCreated => _created;
    public long Revision => _revision;
    public Uuid ProgramId { get; private set; }
    public Uuid ServiceId { get; private set; }
    public string? Kind { get; private set; }
    public string? Identifier { get; private set; }
    public long EffectiveVersionCount => _versions.Count;
    public long? LatestEffectiveRevision => _versions.Count == 0 ? null : _versions[^1].Revision;

    /// <summary>The effective version on a date, or null before the first effective date.</summary>
    public long? EffectiveVersionOn(DateOnly date)
    {
        for (var index = _versions.Count - 1; index >= 0; index--)
            if (_versions[index].EffectiveFrom <= date)
                return _versions[index].Version;
        return null;
    }

    public CommitmentDraft(Uuid tenantId, Uuid draftId)
        : base(draftId, new EventStreamAddress(tenantId.ToString(), "commitment-drafts",
            draftId.ToString()))
    {
        _tenantId = tenantId;
        On<CommitmentDraftCreated>(ev =>
        {
            _created = true;
            _revision = 1;
            ProgramId = ev.ProgramId;
            ServiceId = ev.ServiceId;
            Kind = ev.Kind;
            Identifier = ev.Identifier;
            _createRequestId = ev.CreateRequestId;
            _initialStatement = ev.Statement;
            _initialContext = ev.Context;
            _initialSourceReference = ev.SourceReference;
            _pendingAuthors.Add(ev.ActorMemberId);
        });
        On<CommitmentDraftRevised>(ev =>
        {
            _revision = ev.Revision;
            if (LatestEffectiveRevision is { } effective && effective == ev.Revision - 1)
                _pendingAuthors.Clear();
            _pendingAuthors.Add(ev.ActorMemberId);
        });
        On<CommitmentReviewed>(ev =>
        {
            if (ev.Version is { } version && ev.EffectiveFrom is { } effectiveFrom)
                _versions.Add((version, ev.Revision, effectiveFrom));
        });
    }

    public static string NormalizeIdentifier(string? identifier) =>
        identifier?.Trim().ToUpperInvariant() ?? string.Empty;

    public static string NormalizeKind(string? kind) => kind?.Trim().ToLowerInvariant() ?? string.Empty;

    public static Uuid IdFor(Uuid tenantId, Uuid programId, string kind, string identifier) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, programId.ToString()),
            $"{NormalizeKind(kind)}:{NormalizeIdentifier(identifier)}");

    public Result<CommitmentDraftRegistration> Create(Uuid programId, Uuid createRequestId,
        Uuid serviceId, string kind, string identifier, string statement, string context,
        string sourceReference, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        var normalizedKind = NormalizeKind(kind);
        var normalizedIdentifier = NormalizeIdentifier(identifier);
        var error = Validate(normalizedKind, normalizedIdentifier, statement, context,
            sourceReference);
        if (error is not null)
            return Result<CommitmentDraftRegistration>.Failure(error);
        if (serviceId == Uuid.Empty)
            return Result<CommitmentDraftRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, "A draft requires a governed service."));
        var cleanStatement = statement.Trim();
        var cleanContext = context.Trim();
        var cleanSourceReference = sourceReference.Trim();
        if (_created)
            return ProgramId == programId && ServiceId == serviceId &&
                   _createRequestId == createRequestId &&
                   StringComparer.Ordinal.Equals(Kind, normalizedKind) &&
                   StringComparer.Ordinal.Equals(Identifier, normalizedIdentifier) &&
                   StringComparer.Ordinal.Equals(_initialStatement, cleanStatement) &&
                   StringComparer.Ordinal.Equals(_initialContext, cleanContext) &&
                   StringComparer.Ordinal.Equals(_initialSourceReference, cleanSourceReference)
                ? Result<CommitmentDraftRegistration>.Success(new CommitmentDraftRegistration(
                    Id, normalizedKind, normalizedIdentifier, 1))
                : Result<CommitmentDraftRegistration>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The draft identifier already exists."));
        CommitmentDraftCreated created = new(_tenantId, programId, Id, createRequestId,
            serviceId, normalizedKind, normalizedIdentifier, cleanStatement, cleanContext,
            cleanSourceReference, actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        };
        if (JsonSerializer.SerializeToUtf8Bytes(created,
                ComplianceCoreJsonContext.Default.CommitmentDraftCreated).Length >
            MaximumDraftEventPayloadBytes)
            return Result<CommitmentDraftRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, "The draft exceeds the bounded event payload size."));
        RaiseEvent(created);
        return Result<CommitmentDraftRegistration>.Success(new CommitmentDraftRegistration(
            Id, normalizedKind, normalizedIdentifier, 1));
    }

    public CommandFailure? Revise(Uuid programId, long expectedRevision, string statement,
        string context, string sourceReference, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        if (!_created || ProgramId != programId)
            return CommandFailure.MissingRecord("The draft was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "commitment draft", _revision));
        var error = Validate(Kind!, Identifier!, statement, context, sourceReference);
        if (error is not null)
            return CommandFailure.InvalidContent(error.Message!);
        CommitmentDraftRevised revised = new(_tenantId, programId, Id, _revision + 1,
            statement.Trim(), context.Trim(), sourceReference.Trim(), actorMemberId,
            actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        };
        if (JsonSerializer.SerializeToUtf8Bytes(revised,
                ComplianceCoreJsonContext.Default.CommitmentDraftRevised).Length >
            MaximumDraftEventPayloadBytes)
            return CommandFailure.InvalidContent("The draft exceeds the bounded event payload size.");
        RaiseEvent(revised);
        return null;
    }

    /// <summary>The party that performs a commitment of this kind.</summary>
    public static string PerformedBy(string kind) => kind switch
    {
        "user_entity_responsibility" => "user_entity",
        "subservice_responsibility" => "subservice_organization",
        _ => "service_organization",
    };

    /// <summary>CUECs and CSOCs are never internally performed.</summary>
    public static bool IsInternallyPerformed(string kind) =>
        StringComparer.Ordinal.Equals(PerformedBy(kind), "service_organization");

    public CommandFailure? Review(Uuid programId, long expectedRevision, Uuid decisionId,
        string outcome, string? ownerReference, string? applicability, string? interpretation,
        string? interpretationNote, string rationale, DateOnly? effectiveFrom,
        string? impactDigest, Uuid reviewerMemberId, string reviewerDisplay,
        DateTimeOffset decidedAt, SeparationOfDutiesWaiver? separationOfDutiesWaiver = null)
    {
        if (!_created || ProgramId != programId)
            return CommandFailure.MissingRecord("The draft was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "commitment draft", _revision));
        if (separationOfDutiesWaiver is not null &&
            (separationOfDutiesWaiver.TenantId != _tenantId ||
             !separationOfDutiesWaiver.Allows(new SeparationOfDutiesWaiverScope(
                 SeparationOfDutiesRecordTypes.Commitment, Id, Id, expectedRevision,
                 SeparationOfDutiesActions.Review), reviewerMemberId, decidedAt)))
            return CommandFailure.ActorProhibited(
                "The separation-of-duties waiver is not active for this reviewer and draft revision.");
        if (_pendingAuthors.Contains(reviewerMemberId) && separationOfDutiesWaiver is null)
            return CommandFailure.ActorProhibited(
                "A commitment author cannot review a revision they authored.");
        if (string.IsNullOrWhiteSpace(rationale) || rationale.Length > 4000 ||
            interpretationNote?.Length > 4000)
            return CommandFailure.InvalidContent("A review requires a bounded rationale.");
        long? version = null;
        switch (outcome)
        {
            case "request_changes":
                effectiveFrom = null;
                impactDigest = null;
                break;
            case "accept":
                if (string.IsNullOrWhiteSpace(ownerReference) || ownerReference.Length > 200)
                    return CommandFailure.InvalidContent("Acceptance requires a verified owner.");
                if (applicability is not ("applicable" or "not_applicable"))
                    return CommandFailure.InvalidContent(
                        "Applicability must be applicable or not_applicable.");
                if (interpretation is not ("supported" or "unsupported"))
                    return CommandFailure.InvalidContent(
                        "Interpretation must be supported or unsupported.");
                if (effectiveFrom is null || string.IsNullOrWhiteSpace(impactDigest))
                    return CommandFailure.InvalidContent(
                        "Acceptance requires an effective date and the acknowledged impact digest.");
                if (LatestEffectiveRevision == _revision)
                    return CommandFailure.StateConflict(
                        "This revision is already effective. Revise the draft to propose a successor.");
                if (_versions.Count > 0 && effectiveFrom <= _versions[^1].EffectiveFrom)
                    return CommandFailure.InvalidContent(
                        "A successor must become effective after the previous effective version.");
                version = _versions.Count + 1;
                break;
            default:
                return CommandFailure.InvalidContent("The outcome must be accept or request_changes.");
        }
        RaiseEvent(new CommitmentReviewed(_tenantId, programId, Id, expectedRevision,
            decisionId, outcome, ownerReference?.Trim(), applicability, interpretation,
            string.IsNullOrWhiteSpace(interpretationNote) ? null : interpretationNote.Trim(),
            rationale.Trim(), version, effectiveFrom, impactDigest, reviewerMemberId,
            reviewerDisplay, decidedAt, separationOfDutiesWaiver?.Id)
        {
            StoredActor = ActorReference.ForMember(reviewerMemberId, reviewerDisplay),
        });
        return null;
    }

    static RequestError? Validate(string kind, string identifier, string statement,
        string context, string sourceReference)
    {
        if (!AllowedKinds.Contains(kind))
            return new RequestError(RequestErrorKind.Validation,
                "The draft kind is unsupported.");
        if (identifier.Length is < 1 or > 80 ||
            !identifier.All(static c => char.IsAsciiLetterUpper(c) ||
                char.IsAsciiDigit(c) || c is '-' or '_' or '.'))
            return new RequestError(RequestErrorKind.Validation,
                "An identifier requires 1 to 80 ASCII letters, digits, hyphens, underscores, or periods.");
        if (string.IsNullOrWhiteSpace(statement) || statement.Length > 16000 ||
            context is null || context.Length > 16000 ||
            string.IsNullOrWhiteSpace(sourceReference) || sourceReference.Length > 1024)
            return new RequestError(RequestErrorKind.Validation,
                "A draft requires a bounded statement and source reference.");
        return null;
    }
}
