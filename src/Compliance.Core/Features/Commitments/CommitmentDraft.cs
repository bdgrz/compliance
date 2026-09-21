using System.Text.Json;
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

    public bool IsCreated => _created;
    public long Revision => _revision;
    public Uuid ProgramId { get; private set; }
    public Uuid ServiceId { get; private set; }
    public string? Kind { get; private set; }
    public string? Identifier { get; private set; }

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
        });
        On<CommitmentDraftRevised>(ev => _revision = ev.Revision);
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
            cleanSourceReference, actorMemberId, actorDisplay, changedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(created,
                ComplianceCoreJsonContext.Default.CommitmentDraftCreated).Length >
            MaximumDraftEventPayloadBytes)
            return Result<CommitmentDraftRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, "The draft exceeds the bounded event payload size."));
        RaiseEvent(created);
        return Result<CommitmentDraftRegistration>.Success(new CommitmentDraftRegistration(
            Id, normalizedKind, normalizedIdentifier, 1));
    }

    public Result Revise(Uuid programId, long expectedRevision, string statement,
        string context, string sourceReference, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        if (!_created || ProgramId != programId)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The draft was not found."));
        if (expectedRevision != _revision)
            return Result.Failure(VersionedRecordRules.StaleRevision("commitment draft", _revision));
        var error = Validate(Kind!, Identifier!, statement, context, sourceReference);
        if (error is not null)
            return Result.Failure(error);
        CommitmentDraftRevised revised = new(_tenantId, programId, Id, _revision + 1,
            statement.Trim(), context.Trim(), sourceReference.Trim(), actorMemberId,
            actorDisplay, changedAt);
        if (JsonSerializer.SerializeToUtf8Bytes(revised,
                ComplianceCoreJsonContext.Default.CommitmentDraftRevised).Length >
            MaximumDraftEventPayloadBytes)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The draft exceeds the bounded event payload size."));
        RaiseEvent(revised);
        return Result.Success;
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
