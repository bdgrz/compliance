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

    public bool IsCreated => _created;
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
        });
        On<ControlDraftRevised>(ev => _revision = ev.Revision);
    }

    public static string NormalizeIdentifier(string? identifier) =>
        identifier?.Trim().ToUpperInvariant() ?? string.Empty;

    public static Uuid IdFor(Uuid tenantId, Uuid programId, string identifier) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, programId.ToString()),
            NormalizeIdentifier(identifier));

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
            return ProgramId == programId && _createRequestId == createRequestId &&
                   StringComparer.Ordinal.Equals(_identifier, normalized) &&
                   Equal(_initialContent!, clean)
                ? Result<ControlRegistration>.Success(new ControlRegistration(Id, normalized, 1))
                : Result<ControlRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The control identifier already exists with different draft content."));
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
        if (!_created || ProgramId != programId)
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

    static RequestError? Validate(string identifier, ControlDraftContent? content)
    {
        if (identifier.Length is < 1 or > 80 ||
            !identifier.All(static c => char.IsAsciiLetterUpper(c) ||
                char.IsAsciiDigit(c) || c is '-' or '_' or '.'))
            return new RequestError(RequestErrorKind.Validation,
                "A control identifier requires 1 to 80 ASCII letters, digits, hyphens, underscores, or periods.");
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
        return null;
    }

    static ControlDraftContent Clean(ControlDraftContent content) => new(
        content.Title.Trim(), content.Objective.Trim(), content.Description.Trim(),
        content.ImplementationNarrative.Trim(),
        content.ExpectedEvidenceDescriptions.Select(static value => value.Trim()).ToArray());

    static bool Equal(ControlDraftContent left, ControlDraftContent right) =>
        StringComparer.Ordinal.Equals(left.Title, right.Title) &&
        StringComparer.Ordinal.Equals(left.Objective, right.Objective) &&
        StringComparer.Ordinal.Equals(left.Description, right.Description) &&
        StringComparer.Ordinal.Equals(left.ImplementationNarrative, right.ImplementationNarrative) &&
        left.ExpectedEvidenceDescriptions.SequenceEqual(right.ExpectedEvidenceDescriptions,
            StringComparer.Ordinal);
}
