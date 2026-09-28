using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Owns the independently approved, exact-scope exception to a SoD conflict.</summary>
public sealed class SeparationOfDutiesWaiver : Aggregate
{
    readonly Uuid _tenantId;
    bool _recorded;
    SeparationOfDutiesWaiverScope? _scope;
    Uuid _beneficiaryMemberId;
    Uuid _requesterMemberId;
    string? _requesterDisplay;
    string? _rationale;
    DateTimeOffset _requestedAt;
    DateTimeOffset _expiresAt;
    Uuid _approverMemberId;
    string? _approverDisplay;
    DateTimeOffset? _approvedAt;

    public SeparationOfDutiesWaiver(Uuid tenantId, Uuid waiverId)
        : base(waiverId, new EventStreamAddress(tenantId.ToString(),
            "separation-of-duties-waivers", waiverId.ToString()))
    {
        _tenantId = tenantId;
        On<SeparationOfDutiesWaiverRecorded>(ev =>
        {
            _recorded = true;
            _scope = ev.Scope;
            _beneficiaryMemberId = ev.BeneficiaryMemberId;
            _requesterMemberId = ev.RequesterMemberId;
            _requesterDisplay = ev.RequesterDisplay;
            _rationale = ev.Rationale;
            _requestedAt = ev.RequestedAt;
            _expiresAt = ev.ExpiresAt;
        });
        On<SeparationOfDutiesWaiverApproved>(ev =>
        {
            _approverMemberId = ev.ApproverMemberId;
            _approverDisplay = ev.ApproverDisplay;
            _approvedAt = ev.ApprovedAt;
        });
    }

    public bool IsRecorded => _recorded;

    public Uuid TenantId => _tenantId;

    public Uuid BeneficiaryMemberId => _beneficiaryMemberId;

    public Uuid RequesterMemberId => _requesterMemberId;

    public string RequesterDisplay => _requesterDisplay ?? string.Empty;

    public string Rationale => _rationale ?? string.Empty;

    public DateTimeOffset RequestedAt => _requestedAt;

    public DateTimeOffset ExpiresAt => _expiresAt;

    public Uuid? ApproverMemberId => _approvedAt is null ? null : _approverMemberId;

    public string? ApproverDisplay => _approvedAt is null ? null : _approverDisplay;

    public DateTimeOffset? ApprovedAt => _approvedAt;

    public SeparationOfDutiesWaiverScope? Scope => _scope;

    public CommandFailure? Record(SeparationOfDutiesWaiverScope scope,
        Uuid beneficiaryMemberId, Uuid requesterMemberId, string requesterDisplay,
        string rationale, DateTimeOffset requestedAt, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (_recorded)
        {
            return Equivalent(scope, beneficiaryMemberId, requesterMemberId, requesterDisplay,
                    rationale, expiresAt)
                ? null
                : CommandFailure.StateConflict("The waiver already exists with different terms.");
        }

        if (_tenantId == Uuid.Empty || Id == Uuid.Empty ||
            string.IsNullOrWhiteSpace(scope.RecordType) || scope.RecordId == Uuid.Empty ||
            scope.VersionId == Uuid.Empty || scope.Revision <= 0 ||
            string.IsNullOrWhiteSpace(scope.Action) ||
            beneficiaryMemberId == Uuid.Empty || requesterMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(requesterDisplay) || string.IsNullOrWhiteSpace(rationale))
            return CommandFailure.InvalidContent(
                "A separation-of-duties waiver requires a scoped record, beneficiary, Org Admin, and rationale.");
        if (expiresAt <= requestedAt)
            return CommandFailure.InvalidContent(
                "A separation-of-duties waiver must expire after it is requested.");

        var normalizedRationale = rationale.Trim();
        var normalizedDisplay = requesterDisplay.Trim();
        RaiseEvent(new SeparationOfDutiesWaiverRecorded(_tenantId, Id, scope,
            beneficiaryMemberId, requesterMemberId, normalizedDisplay, normalizedRationale,
            requestedAt, expiresAt)
        {
            StoredActor = ActorReference.ForMember(requesterMemberId, normalizedDisplay),
        });
        return null;
    }

    public CommandFailure? Approve(Uuid approverMemberId, string approverDisplay,
        DateTimeOffset approvedAt)
    {
        if (!_recorded)
            return CommandFailure.MissingRecord("The separation-of-duties waiver was not found.");
        if (approverMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(approverDisplay))
            return CommandFailure.InvalidContent("A waiver approval requires an Org Admin.");
        if (approverMemberId == _requesterMemberId || approverMemberId == _beneficiaryMemberId)
            return CommandFailure.ActorProhibited(
                "A waiver must be approved by a different Org Admin who is not its beneficiary.");
        if (_approvedAt is not null)
            return _approverMemberId == approverMemberId &&
                   StringComparer.Ordinal.Equals(_approverDisplay, approverDisplay.Trim())
                ? null
                : CommandFailure.StateConflict(
                    "The separation-of-duties waiver has already been approved.");
        if (approvedAt < _requestedAt || approvedAt >= _expiresAt)
            return CommandFailure.StateConflict("The separation-of-duties waiver is outside its approval window.");

        var normalizedDisplay = approverDisplay.Trim();
        RaiseEvent(new SeparationOfDutiesWaiverApproved(_tenantId, Id,
            approverMemberId, normalizedDisplay, approvedAt)
        {
            StoredActor = ActorReference.ForMember(approverMemberId, normalizedDisplay),
        });
        return null;
    }

    public bool Allows(SeparationOfDutiesWaiverScope scope, Uuid beneficiaryMemberId,
        DateTimeOffset at) => _recorded && _approvedAt is { } approvedAt &&
        at >= approvedAt && at < _expiresAt && beneficiaryMemberId == _beneficiaryMemberId &&
        _scope == scope;

    public SeparationOfDutiesWaiverView? ViewOrNull(DateTimeOffset now) =>
        _recorded ? ToView(now) : null;

    public SeparationOfDutiesWaiverView ToView(DateTimeOffset now)
    {
        if (!_recorded || _scope is null)
            throw new InvalidOperationException("An unrecorded waiver has no public view.");
        var approved = _approvedAt is not null;
        var active = approved && now >= _approvedAt && now < _expiresAt;
        return new SeparationOfDutiesWaiverView(_tenantId, Id, _scope,
            _beneficiaryMemberId, _requesterMemberId, RequesterDisplay,
            Rationale, _requestedAt, _expiresAt, ApproverMemberId, ApproverDisplay,
            _approvedAt, now >= _expiresAt ? "expired" : active ? "active" : "pending", active)
        {
            Requester = ActorReference.ForMember(_requesterMemberId, RequesterDisplay),
            Approver = ApproverMemberId is { } approver && ApproverDisplay is { } display
                ? ActorReference.ForMember(approver, display)
                : null,
        };
    }

    bool Equivalent(SeparationOfDutiesWaiverScope scope, Uuid beneficiaryMemberId,
        Uuid requesterMemberId, string requesterDisplay, string rationale,
        DateTimeOffset expiresAt) =>
        _scope == scope && _beneficiaryMemberId == beneficiaryMemberId &&
        _requesterMemberId == requesterMemberId &&
        StringComparer.Ordinal.Equals(_requesterDisplay, requesterDisplay.Trim()) &&
        StringComparer.Ordinal.Equals(_rationale, rationale.Trim()) &&
        _expiresAt == expiresAt;
}
