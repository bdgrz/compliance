using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Canonical policy and legal-hold authority for an existing tenant source, never its content store.</summary>
public sealed class ArtifactRetention : Aggregate
{
    public const string Policy = "m0_d16_v1";
    readonly Uuid _tenantId;
    readonly string _sourceKind;
    readonly Uuid _sourceId;
    readonly Dictionary<Uuid, ArtifactLegalHoldView> _holds = [];
    ulong _sourcePosition;
    ArtifactRetentionSource? _source;
    ArtifactRetentionBasisRecorded? _basis;

    public long Revision { get; private set; }
    public ArtifactRetentionBasisRecorded? Basis => _basis;
    public ArtifactRetentionSource? Source => _source;

    public ArtifactRetention(Uuid tenantId, string sourceKind, Uuid sourceId)
        : base(IdFor(tenantId, sourceKind, sourceId), new EventStreamAddress(tenantId.ToString(),
            "artifact_retention", IdFor(tenantId, sourceKind, sourceId).ToString()))
    {
        _tenantId = tenantId;
        _sourceKind = sourceKind;
        _sourceId = sourceId;
        On<ArtifactRetentionBound>(ev =>
        {
            if (Revision != 0 || ev.Revision != 1 || !MatchesScope(ev.Source) || ev.SourcePosition == 0 ||
                !ValidActor(ev.RecordedBy) || ev.RecordedAt == default)
                throw new InvalidOperationException("The retained source binding is invalid.");
            _source = ev.Source;
            Revision = ev.Revision;
            _sourcePosition = ev.SourcePosition;
        });
        On<ArtifactRetentionBasisRecorded>(ev =>
        {
            if (_basis is not null || !ValidContinuation(ev.Source, ev.Revision, ev.SourcePosition, ev.RecordedBy,
                    ev.Reason, ev.RecordedAt) || ev.Policy != Policy || !ValidPeriod(ev.PeriodStart, ev.PeriodEnd) ||
                ev.RetainsThrough != ev.PeriodEnd.AddYears(7) || ev.BasisKind != BasisKind(ev.Source.SourceKind))
                throw new InvalidOperationException("The retained source basis is invalid.");
            _basis = ev;
            Revision = ev.Revision;
            _sourcePosition = ev.SourcePosition;
        });
        On<ArtifactLegalHoldPlaced>(ev =>
        {
            if (!ValidContinuation(ev.Source, ev.Revision, ev.SourcePosition, ev.RecordedBy, ev.Reason, ev.RecordedAt) ||
                ev.HoldId == Uuid.Empty || _holds.ContainsKey(ev.HoldId))
                throw new InvalidOperationException("The retained source legal hold is invalid.");
            _holds.Add(ev.HoldId, new(ev.HoldId, ev.Reason, ev.RecordedBy, ev.RecordedAt));
            Revision = ev.Revision;
            _sourcePosition = ev.SourcePosition;
        });
        On<ArtifactLegalHoldReleased>(ev =>
        {
            if (!ValidContinuation(ev.Source, ev.Revision, ev.SourcePosition, ev.RecordedBy, ev.Reason, ev.RecordedAt) ||
                !_holds.TryGetValue(ev.HoldId, out var hold) || hold.ReleasedAt is not null || ev.RecordedAt < hold.PlacedAt)
                throw new InvalidOperationException("The retained source legal hold release is invalid.");
            _holds[ev.HoldId] = hold with { ReleaseReason = ev.Reason, ReleasedBy = ev.RecordedBy, ReleasedAt = ev.RecordedAt };
            Revision = ev.Revision;
            _sourcePosition = ev.SourcePosition;
        });
    }

    public static Uuid IdFor(Uuid tenantId, string sourceKind, Uuid sourceId)
    {
        if (tenantId == Uuid.Empty || sourceId == Uuid.Empty || !IsSupportedSource(sourceKind))
            throw new ArgumentException("Retention requires an existing supported tenant source.");
        return Uuid.CreateVersion5(tenantId, $"artifact_retention:{sourceKind}:{sourceId}");
    }

    public static bool IsSupportedSource(string sourceKind) => sourceKind is "evidence_artifact" or "application_import";

    public IReadOnlyList<ArtifactLegalHoldView> GetLegalHolds() => Array.AsReadOnly(_holds.Values.ToArray());

    public Result RecordBasis(ArtifactRetentionSourceSnapshot source, long expectedRevision,
        DateOnly? periodStart, DateOnly? periodEnd, string reason, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (CheckSource(source) is { } error)
            return Result.Failure(error);
        if (!ValidActor(actor) || !ValidReason(reason) || recordedAt == default || expectedRevision < 0)
            return InvalidDecision();
        if ((periodStart is null) != (periodEnd is null))
            return InvalidPeriod();
        if (_sourceKind == "evidence_artifact")
        {
            if (source.PeriodStart is not { } nativeStart || source.PeriodEnd is not { } nativeEnd ||
                (periodStart is { } start && start != nativeStart) || (periodEnd is { } end && end != nativeEnd))
                return InvalidPeriod();
            periodStart = nativeStart;
            periodEnd = nativeEnd;
        }
        if (periodStart is not { } first || periodEnd is not { } last || !ValidPeriod(first, last))
            return InvalidPeriod();
        var normalized = reason.Trim();
        if (_basis is not null)
            return _basis.PeriodStart == first && _basis.PeriodEnd == last && _basis.Reason == normalized &&
                   expectedRevision <= Revision ? Result.Success : ChangedIntent();
        if (expectedRevision != Revision)
            return StaleRevision();
        Bind(source, actor, recordedAt);
        RaiseEvent(new ArtifactRetentionBasisRecorded(source.Source, Revision + 1, source.Position,
            Policy, first, last, last.AddYears(7), BasisKind(_sourceKind), normalized, actor, recordedAt));
        return Result.Success;
    }

    public Result PlaceLegalHold(ArtifactRetentionSourceSnapshot source, long expectedRevision, Uuid holdId,
        string reason, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (CheckSource(source) is { } error)
            return Result.Failure(error);
        if (holdId == Uuid.Empty || !ValidActor(actor) || !ValidReason(reason) || recordedAt == default || expectedRevision < 0)
            return InvalidDecision();
        var normalized = reason.Trim();
        if (_holds.TryGetValue(holdId, out var previous))
            return previous.Reason == normalized && expectedRevision <= Revision ? Result.Success : ChangedIntent();
        if (expectedRevision != Revision)
            return StaleRevision();
        Bind(source, actor, recordedAt);
        RaiseEvent(new ArtifactLegalHoldPlaced(source.Source, Revision + 1, source.Position,
            holdId, normalized, actor, recordedAt));
        return Result.Success;
    }

    public Result ReleaseLegalHold(ArtifactRetentionSourceSnapshot source, long expectedRevision, Uuid holdId,
        string reason, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (CheckSource(source) is { } error)
            return Result.Failure(error);
        if (holdId == Uuid.Empty || !ValidActor(actor) || !ValidReason(reason) || recordedAt == default || expectedRevision < 0)
            return InvalidDecision();
        if (!_holds.TryGetValue(holdId, out var hold))
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The legal hold was not found."));
        var normalized = reason.Trim();
        if (hold.ReleasedAt is not null)
            return hold.ReleaseReason == normalized && expectedRevision <= Revision ? Result.Success : ChangedIntent();
        if (expectedRevision != Revision)
            return StaleRevision();
        if (recordedAt < hold.PlacedAt)
            return InvalidDecision();
        RaiseEvent(new ArtifactLegalHoldReleased(source.Source, Revision + 1, source.Position,
            holdId, normalized, actor, recordedAt));
        return Result.Success;
    }

    public Result ValidateSource(ArtifactRetentionSourceSnapshot source) => CheckSource(source) is { } error
        ? Result.Failure(error) : Result.Success;

    public ArtifactRetentionView Assess(ArtifactRetentionSource source, DateOnly assessedAt)
    {
        if (!MatchesScope(source) || (_source is not null && _source != source))
            throw new ArgumentException("Assessment requires the exact retained source.", nameof(source));
        var holds = GetLegalHolds();
        var active = holds.Count(hold => hold.ReleasedAt is null);
        var elapsed = _basis is not null && assessedAt > _basis.RetainsThrough;
        var blockers = new List<string>();
        if (_basis is null)
        {
            blockers.Add("retention_basis_unrecorded");
            blockers.Add("supported_period_unrecorded");
        }
        else if (!elapsed)
            blockers.Add("retention_unelapsed");
        if (active > 0)
            blockers.Add("active_legal_hold");
        blockers.Add("engagement_hold_authority_unavailable");
        blockers.Add("historical_reliance_unavailable");
        blockers.Add("disposition_approval_unavailable");
        blockers.Add(_sourceKind == "application_import" ? "inline_disposition_unsupported" : "storage_hold_enforcement_pending");
        return new(source, Revision, _basis?.Policy, _basis?.PeriodStart, _basis?.PeriodEnd, _basis?.RetainsThrough,
            elapsed, active, holds.Count, false, blockers.AsReadOnly());
    }

    void Bind(ArtifactRetentionSourceSnapshot source, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (_source is null)
            RaiseEvent(new ArtifactRetentionBound(source.Source, Revision + 1, source.Position, actor, recordedAt));
    }

    RequestError? CheckSource(ArtifactRetentionSourceSnapshot source)
    {
        if (!MatchesScope(source.Source))
            return new(RequestErrorKind.NotFound, "The retained source was not found in this tenant.");
        if (source.Position == 0 || source.Position < _sourcePosition)
            return new(RequestErrorKind.Conflict, "The retained source must be durable.", isTransient: true);
        if (_source is not null && _source != source.Source)
            return new(RequestErrorKind.Conflict, "The retained source content differs from its recorded binding.");
        if (_basis is { BasisKind: "source_period" } &&
            (source.PeriodStart != _basis.PeriodStart || source.PeriodEnd != _basis.PeriodEnd))
            return new(RequestErrorKind.Conflict, "The retained evidence period differs from its authoritative source.");
        return null;
    }

    bool MatchesScope(ArtifactRetentionSource source) => source.TenantId == _tenantId && source.SourceKind == _sourceKind &&
        source.SourceId == _sourceId && source.ContentSha256 is { Length: 64 } &&
        source.ContentSha256.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    bool ValidContinuation(ArtifactRetentionSource source, long revision, ulong position, ActorReference actor,
        string reason, DateTimeOffset at) => _source == source && MatchesScope(source) && revision == Revision + 1 &&
        position > 0 && position >= _sourcePosition && ValidActor(actor) && ValidReason(reason) && reason == reason.Trim() && at != default;

    static bool ValidActor(ActorReference actor) => actor.Kind == "member" && Uuid.TryParse(actor.Id, null, out var id) &&
        id != Uuid.Empty && !string.IsNullOrWhiteSpace(actor.Display) && actor.Display.Length <= 2000;

    static bool ValidReason(string reason) => !string.IsNullOrWhiteSpace(reason) && reason.Length <= 2000;
    static bool ValidPeriod(DateOnly first, DateOnly last) => first <= last && last.Year <= 9992;
    static string BasisKind(string sourceKind) => sourceKind == "evidence_artifact" ? "source_period" : "admin_period";
    Result StaleRevision() => Result.Failure(new RequestError(RequestErrorKind.Conflict, $"The retention revision is {Revision}."));
    static Result ChangedIntent() => Result.Failure(new RequestError(RequestErrorKind.Conflict, "The recorded retention decision has different intent."));
    static Result InvalidPeriod() => Result.Failure(new RequestError(RequestErrorKind.Validation, "Retention requires the exact explicit supported period within policy date bounds."));
    static Result InvalidDecision() => Result.Failure(new RequestError(RequestErrorKind.Validation, "Retention requires a bounded attributed personal decision."));
}
