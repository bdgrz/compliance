using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class ControlDraft : Aggregate
{
    // Fitz stream events use a u16 frame. Reserve room for the Portia envelope,
    // stream address, metadata, and framing after the serialized domain event.
    public const int MaximumDraftEventPayloadBytes = 48 * 1024;
    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    string? _identifier;
    Uuid _createRequestId;
    ControlDraftContent? _initialContent;
    ControlDraftContent? _currentContent;
    bool _discarded;

    public bool IsCreated => _created;
    public bool IsVisible => _created && !_discarded;
    public long Revision => _revision;
    public Uuid ProgramId { get; private set; }

    public ControlDraft(Uuid tenantId, Uuid controlId)
        : base(controlId, new EventStreamAddress(tenantId.ToString(), "controls", controlId.ToString()))
    {
        _tenantId = tenantId;
        On<ControlDraftCreated>(ev =>
        {
            _created = true;
            _revision = 1;
            ProgramId = ev.ProgramId;
            _identifier = ev.Identifier;
            _createRequestId = ev.CreateRequestId;
            _initialContent = ev.Content;
            _currentContent = ev.Content;
        });
        On<ControlDraftRevised>(ev =>
        {
            _revision = ev.Revision;
            _currentContent = ev.Content;
        });
        On<ControlDraftDiscarded>(ev =>
        {
            _revision = ev.Revision;
            _discarded = true;
        });
    }

    public static string NormalizeIdentifier(string? identifier) =>
        identifier?.Trim().ToUpperInvariant() ?? string.Empty;

    public static Uuid IdFor(Uuid tenantId, Uuid programId, string identifier) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, programId.ToString()),
            NormalizeIdentifier(identifier));

    internal static RequestError? ValidateCreateInput(string? identifier,
        ControlDraftContent? content) => Validate(NormalizeIdentifier(identifier), content);

    public Result<ControlRegistration> Create(Uuid programId, Uuid createRequestId, string identifier,
        ControlDraftContent content, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        var normalized = NormalizeIdentifier(identifier);
        var error = Validate(normalized, content);
        if (error is not null)
            return Result<ControlRegistration>.Failure(error);
        var clean = Clean(content);
        if (_created)
        {
            if (_discarded)
                return Result<ControlRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The discarded control draft cannot be recreated."));
            return ProgramId == programId && _createRequestId == createRequestId &&
                   StringComparer.Ordinal.Equals(_identifier, normalized) &&
                   Equal(_initialContent!, clean)
                ? Result<ControlRegistration>.Success(new ControlRegistration(Id, normalized, 1))
                : Result<ControlRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The control identifier already exists with different draft content."));
        }
        ControlDraftCreated created = new(_tenantId, programId, Id, createRequestId,
            normalized, clean, actorMemberId, actorDisplay, changedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(created,
                ComplianceCoreJsonContext.Default.ControlDraftCreated).Length >
            MaximumDraftEventPayloadBytes)
            return Result<ControlRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "The control draft exceeds the bounded event payload size."));
        RaiseEvent(created);
        return Result<ControlRegistration>.Success(new ControlRegistration(Id, normalized, 1));
    }

    public Result Revise(Uuid programId, long expectedRevision, ControlDraftContent content,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created || _discarded || ProgramId != programId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control draft was not found."));
        if (expectedRevision != _revision)
            return Result.Failure(VersionedRecordRules.StaleRevision("control draft", _revision));
        var error = Validate(_identifier!, content);
        if (error is not null)
            return Result.Failure(error);
        ControlDraftRevised revised = new(_tenantId, programId, Id, _revision + 1,
            Clean(content), actorMemberId, actorDisplay, changedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(revised,
                ComplianceCoreJsonContext.Default.ControlDraftRevised).Length >
            MaximumDraftEventPayloadBytes)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The control draft exceeds the bounded event payload size."));
        RaiseEvent(revised);
        return Result.Success;
    }

    public Result Discard(Uuid programId, long expectedRevision, string rationale,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset discardedAt)
    {
        if (!_created || _discarded || ProgramId != programId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control draft was not found."));
        if (expectedRevision != _revision)
            return Result.Failure(VersionedRecordRules.StaleRevision("control draft", _revision));
        if (_currentContent?.Applicability is { Count: > 0 })
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Discarding a control draft requires no retained applicability relationships."));
        if (string.IsNullOrWhiteSpace(rationale))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Discarding a control draft requires a rationale."));
        var discarded = new ControlDraftDiscarded(_tenantId, programId, Id, _revision,
            actorMemberId, actorDisplay, rationale.Trim(), discardedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(discarded,
                ComplianceCoreJsonContext.Default.ControlDraftDiscarded).Length >
            MaximumDraftEventPayloadBytes)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The control draft discard exceeds the bounded event payload size."));
        RaiseEvent(discarded);
        return Result.Success;
    }

    static RequestError? Validate(string identifier, ControlDraftContent? content)
    {
        if (identifier.Length is < 1 or > 80 ||
            !identifier.All(static c => char.IsAsciiLetterUpper(c) ||
                char.IsAsciiDigit(c) || c is '-' or '_' or '.'))
            return new RequestError(RequestErrorKind.Validation,
                "A control identifier requires 1 to 80 ASCII letters, digits, hyphens, underscores, or periods.");
        return ValidateContent(content);
    }

    internal static RequestError? ValidateContent(ControlDraftContent? content)
    {
        if (content is null || content.ExpectedEvidenceDescriptions is null ||
            string.IsNullOrWhiteSpace(content.Title) ||
            string.IsNullOrWhiteSpace(content.Objective) ||
            string.IsNullOrWhiteSpace(content.Description) ||
            string.IsNullOrWhiteSpace(content.ImplementationNarrative))
            return new RequestError(RequestErrorKind.Validation,
                "A control draft requires title, objective, description, implementation narrative, and expected evidence descriptions.");
        if (content.Title.Length > 200 || content.Objective.Length > 2000 ||
            content.Description.Length > 8000 || content.ImplementationNarrative.Length > 12000 ||
            content.ExpectedEvidenceDescriptions.Count is < 1 or > 20 ||
            content.ExpectedEvidenceDescriptions.Any(static value =>
                string.IsNullOrWhiteSpace(value) || value.Length > 2000))
            return new RequestError(RequestErrorKind.Validation,
                "The control draft exceeds its content limits or has an empty expected evidence description.");
        if (content.OwnerReference is { Length: > 500 })
            return new RequestError(RequestErrorKind.Validation,
                "A control owner reference must be at most 500 characters.");
        var applicability = content.Applicability ?? [];
        if (applicability.Count > 100 || applicability.Any(static reference =>
                !IsValidApplicabilityReference(reference)) ||
            applicability.Select(static reference => reference.EntryId).Distinct().Count() !=
            applicability.Count)
            return new RequestError(RequestErrorKind.Validation,
            "Every control applicability reference requires a unique ID, typed subject, rationale, and a consistent governed reference state.");
        return null;
    }

    static bool IsValidApplicabilityReference(ControlApplicabilityReference? reference)
    {
        if (reference is null || reference.EntryId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(reference.Subject) || reference.Subject.Length > 500 ||
            string.IsNullOrWhiteSpace(reference.Rationale) || reference.Rationale.Length > 2000)
            return false;
        return reference.SubjectType switch
        {
            "application" or "system_instance" => !reference.Unresolved &&
                reference.GovernedRecordId is { } recordId && recordId != Uuid.Empty,
            "risk" or "process" => reference.Unresolved && reference.GovernedRecordId is null,
            _ => false,
        };
    }

    static ControlDraftContent Clean(ControlDraftContent content) => new(
        content.Title.Trim(), content.Objective.Trim(), content.Description.Trim(),
        content.ImplementationNarrative.Trim(),
        content.ExpectedEvidenceDescriptions.Select(static value => value.Trim()).ToArray(),
        string.IsNullOrWhiteSpace(content.OwnerReference) ? null : content.OwnerReference.Trim(),
        (content.Applicability ?? []).Select(static reference => new ControlApplicabilityReference(
            reference.EntryId, reference.SubjectType, reference.Subject.Trim(),
            reference.GovernedRecordId, reference.Rationale.Trim(), reference.Unresolved)).ToArray());

    static bool Equal(ControlDraftContent left, ControlDraftContent right) =>
        StringComparer.Ordinal.Equals(left.Title, right.Title) &&
        StringComparer.Ordinal.Equals(left.Objective, right.Objective) &&
        StringComparer.Ordinal.Equals(left.Description, right.Description) &&
        StringComparer.Ordinal.Equals(left.ImplementationNarrative, right.ImplementationNarrative) &&
        left.ExpectedEvidenceDescriptions.SequenceEqual(right.ExpectedEvidenceDescriptions,
            StringComparer.Ordinal) &&
        StringComparer.Ordinal.Equals(left.OwnerReference, right.OwnerReference) &&
        (left.Applicability ?? []).SequenceEqual(right.Applicability ?? []);
}
