using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class RiskDraft : Aggregate
{
    // Fitz stream events use a u16 frame. Reserve room for the Portia envelope,
    // stream address, metadata, and framing after the serialized domain event.
    public const int MaximumDraftEventPayloadBytes = 48 * 1024;
    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    string? _identifier;
    Uuid _createRequestId;
    RiskDraftContent? _initialContent;

    public bool IsCreated => _created;
    public long Revision => _revision;
    public Uuid ProgramId { get; private set; }

    public RiskDraft(Uuid tenantId, Uuid riskId)
        : base(riskId, new EventStreamAddress(tenantId.ToString(), "risks", riskId.ToString()))
    {
        _tenantId = tenantId;
        On<RiskDraftCreated>(ev =>
        {
            _created = true;
            _revision = 1;
            ProgramId = ev.ProgramId;
            _identifier = ev.Identifier;
            _createRequestId = ev.CreateRequestId;
            _initialContent = ev.Content;
        });
        On<RiskDraftRevised>(ev => _revision = ev.Revision);
    }

    public static string NormalizeIdentifier(string? identifier) =>
        identifier?.Trim().ToUpperInvariant() ?? string.Empty;

    public static Uuid IdFor(Uuid tenantId, Uuid programId, string identifier) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, programId.ToString()),
            NormalizeIdentifier(identifier));

    public Result<RiskRegistration> Create(Uuid programId, Uuid createRequestId, string identifier,
        RiskDraftContent content, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        var normalized = NormalizeIdentifier(identifier);
        var error = Validate(normalized, content);
        if (error is not null)
            return Result<RiskRegistration>.Failure(error);
        var clean = Clean(content);
        if (_created)
            return ProgramId == programId && _createRequestId == createRequestId &&
                   StringComparer.Ordinal.Equals(_identifier, normalized) &&
                   Equal(_initialContent!, clean)
                ? Result<RiskRegistration>.Success(new RiskRegistration(Id, normalized, 1))
                : Result<RiskRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The risk identifier already exists with different draft content."));
        RiskDraftCreated created = new(_tenantId, programId, Id, createRequestId,
            normalized, clean, actorMemberId, actorDisplay, changedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(created,
                ComplianceCoreJsonContext.Default.RiskDraftCreated).Length >
            MaximumDraftEventPayloadBytes)
            return Result<RiskRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "The risk draft exceeds the bounded event payload size."));
        RaiseEvent(created);
        return Result<RiskRegistration>.Success(new RiskRegistration(Id, normalized, 1));
    }

    public Result Revise(Uuid programId, long expectedRevision, RiskDraftContent content,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created || ProgramId != programId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The risk draft was not found."));
        if (expectedRevision != _revision)
            return Result.Failure(VersionedRecordRules.StaleRevision("risk draft", _revision)
                .ToRequestError());
        var error = Validate(_identifier!, content);
        if (error is not null)
            return Result.Failure(error);
        RiskDraftRevised revised = new(_tenantId, programId, Id, _revision + 1,
            Clean(content), actorMemberId, actorDisplay, changedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(revised,
                ComplianceCoreJsonContext.Default.RiskDraftRevised).Length >
            MaximumDraftEventPayloadBytes)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The risk draft exceeds the bounded event payload size."));
        RaiseEvent(revised);
        return Result.Success;
    }

    static RequestError? Validate(string identifier, RiskDraftContent? content)
    {
        if (identifier.Length is < 1 or > 80 ||
            !identifier.All(static c => char.IsAsciiLetterUpper(c) ||
                char.IsAsciiDigit(c) || c is '-' or '_' or '.'))
            return new RequestError(RequestErrorKind.Validation,
                "A risk identifier requires 1 to 80 ASCII letters, digits, hyphens, underscores, or periods.");
        if (content is null ||
            string.IsNullOrWhiteSpace(content.Title) ||
            string.IsNullOrWhiteSpace(content.Scenario) ||
            string.IsNullOrWhiteSpace(content.PotentialEffect))
            return new RequestError(RequestErrorKind.Validation,
                "A risk draft requires a title, scenario, and potential effect.");
        if (content.Title.Length > 200 || content.Scenario.Length > 8000 ||
            content.PotentialEffect.Length > 8000 || content.SourceNote?.Length > 4000)
            return new RequestError(RequestErrorKind.Validation,
                "The risk draft exceeds its content limits.");
        return null;
    }

    static RiskDraftContent Clean(RiskDraftContent content) => new(
        content.Title.Trim(), content.Scenario.Trim(), content.PotentialEffect.Trim(),
        string.IsNullOrWhiteSpace(content.SourceNote) ? null : content.SourceNote.Trim());

    static bool Equal(RiskDraftContent left, RiskDraftContent right) =>
        StringComparer.Ordinal.Equals(left.Title, right.Title) &&
        StringComparer.Ordinal.Equals(left.Scenario, right.Scenario) &&
        StringComparer.Ordinal.Equals(left.PotentialEffect, right.PotentialEffect) &&
        StringComparer.Ordinal.Equals(left.SourceNote, right.SourceNote);
}
