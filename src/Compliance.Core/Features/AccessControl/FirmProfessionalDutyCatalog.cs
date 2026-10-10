using System.Globalization;
using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Global, source-referenced professional-duty metadata. Operator status is only record authority.</summary>
public sealed class FirmProfessionalDutyCatalog : Aggregate
{
    static readonly Uuid CatalogId = Uuid.Parse("c1f97f68-a902-4dc2-9e8e-b5fd28e34f5d", CultureInfo.InvariantCulture);
    readonly Dictionary<Uuid, FirmProfessionalDutyDesignationView> _designations = [];
    readonly Dictionary<Uuid, (string Intent, ActorReference Actor, FirmProfessionalDutyDesignationView Response)> _decisions = [];

    public FirmProfessionalDutyCatalog() : base(CatalogId,
        new EventStreamAddress("bdgrz", "firm-professional-duties", CatalogId.ToString()))
    {
        On<FirmProfessionalDutyDesignationRecorded>(Apply);
        On<FirmProfessionalDutyDesignationRevoked>(Apply);
    }

    public long Sequence { get; private set; }
    public IReadOnlyList<FirmProfessionalDutyDesignationView> Designations => Array.AsReadOnly(_designations.Values
        .OrderBy(item => item.DesignationId.ToString(), StringComparer.Ordinal).ToArray());
    public FirmProfessionalDutyCatalogView View() => new(Sequence, Designations);

    public FirmProfessionalDutyDesignationView? ActiveDesignation(Uuid userId, string duty, Uuid? tenantId,
        DateTimeOffset at) => _designations.Values.SingleOrDefault(item => item.IsActive && item.UserId == userId &&
            item.Duty == duty && item.TenantId == tenantId && item.DesignatedAt <= at);

    public bool IsDesignated(Uuid userId, string duty, Uuid? tenantId, DateTimeOffset at) =>
        userId != Uuid.Empty && IsSupportedDutyScope(duty, tenantId) &&
        ActiveDesignation(userId, duty, tenantId, at) is not null;

    public Result<FirmProfessionalDutyDesignationView> Designate(Uuid requestId, Uuid designationId,
        FirmStaffMemberView staff, string duty, Uuid? tenantId, string sourceReference, long expectedSequence,
        ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!ValidAttribution(requestId, actor, recordedAt) || designationId == Uuid.Empty ||
            staff is not { IsActive: true, Revision: > 0 } || staff.StaffMemberId == Uuid.Empty ||
            staff.UserId == Uuid.Empty || !IsSupportedDutyScope(duty, tenantId) || !Bounded(sourceReference, 2000))
            return Failure(RequestErrorKind.Validation,
                "A designation requires an active canonical staff identity, supported duty scope and source reference.");

        var request = new RecordFirmProfessionalDutyDesignation(designationId, staff.StaffMemberId, duty,
            tenantId, sourceReference, expectedSequence);
        if (Retry(requestId, request, actor) is { } retry)
            return retry;
        if (expectedSequence != Sequence || _designations.ContainsKey(designationId) ||
            _designations.Values.Any(item => item.IsActive && item.UserId == staff.UserId &&
                item.Duty == duty && item.TenantId == tenantId))
            return Failure(RequestErrorKind.Conflict,
                "Reload the professional-duty catalog; designation IDs and active person/duty scopes are unique.");

        var designation = new FirmProfessionalDutyDesignationView(designationId, staff.StaffMemberId,
            staff.UserId, duty, tenantId, sourceReference, staff.Revision, true, 1, actor, recordedAt,
            actor, recordedAt, null);
        RaiseEvent(new FirmProfessionalDutyDesignationRecorded(requestId, expectedSequence, designation));
        return Result<FirmProfessionalDutyDesignationView>.Success(designation);
    }

    public Result<FirmProfessionalDutyDesignationView> Revoke(Uuid requestId, Uuid designationId,
        long expectedSequence, string reason, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!ValidAttribution(requestId, actor, recordedAt) || designationId == Uuid.Empty || !Bounded(reason, 2000))
            return Failure(RequestErrorKind.Validation,
                "A designation revocation requires an attributed operator and bounded reason.");

        var request = new RevokeFirmProfessionalDutyDesignation(designationId, expectedSequence, reason);
        if (Retry(requestId, request, actor) is { } retry)
            return retry;
        if (expectedSequence != Sequence || !_designations.TryGetValue(designationId, out var current) ||
            !current.IsActive)
            return Failure(RequestErrorKind.Conflict,
                "Reload the professional-duty catalog; only an active retained designation can be revoked.");

        var ev = new FirmProfessionalDutyDesignationRevoked(requestId, expectedSequence, designationId,
            actor, reason, recordedAt);
        RaiseEvent(ev);
        return Result<FirmProfessionalDutyDesignationView>.Success(_designations[designationId]);
    }

    void Apply(FirmProfessionalDutyDesignationRecorded ev)
    {
        var item = ev.Designation;
        if (ev.ExpectedSequence != Sequence || item.DesignationId == Uuid.Empty ||
            item.StaffMemberId == Uuid.Empty || item.UserId == Uuid.Empty || !item.IsActive ||
            item.Revision != 1 || item.DirectoryStaffRevision <= 0 ||
            !IsSupportedDutyScope(item.Duty, item.TenantId) || !Bounded(item.SourceReference, 2000) ||
            !ValidAttribution(ev.RequestId, item.DesignatedBy, item.DesignatedAt) ||
            item.DesignatedBy != item.LastChangedBy || item.DesignatedAt != item.LastChangedAt ||
            item.ChangeReason is not null || _designations.ContainsKey(item.DesignationId) ||
            _designations.Values.Any(existing => existing.IsActive && existing.UserId == item.UserId &&
                existing.Duty == item.Duty && existing.TenantId == item.TenantId))
            throw new InvalidOperationException("A professional-duty designation must retain its first canonical source and sequence.");

        var request = new RecordFirmProfessionalDutyDesignation(item.DesignationId, item.StaffMemberId,
            item.Duty, item.TenantId, item.SourceReference, ev.ExpectedSequence);
        _designations.Add(item.DesignationId, item);
        _decisions.Add(ev.RequestId, (Intent(request), item.DesignatedBy, item));
        Sequence++;
    }

    void Apply(FirmProfessionalDutyDesignationRevoked ev)
    {
        if (ev.ExpectedSequence != Sequence || !_designations.TryGetValue(ev.DesignationId, out var current) ||
            !current.IsActive || !ValidAttribution(ev.RequestId, ev.Actor, ev.RecordedAt) || !Bounded(ev.Reason, 2000))
            throw new InvalidOperationException("A professional-duty revocation must target one active retained designation.");

        var request = new RevokeFirmProfessionalDutyDesignation(ev.DesignationId, ev.ExpectedSequence, ev.Reason);
        var updated = current with
        {
            IsActive = false,
            Revision = current.Revision + 1,
            LastChangedBy = ev.Actor,
            LastChangedAt = ev.RecordedAt,
            ChangeReason = ev.Reason
        };
        _designations[ev.DesignationId] = updated;
        _decisions.Add(ev.RequestId, (Intent(request), ev.Actor, updated));
        Sequence++;
    }

    Result<FirmProfessionalDutyDesignationView>? Retry(Uuid requestId, object request, ActorReference actor) =>
        _decisions.TryGetValue(requestId, out var previous)
            ? previous.Intent == Intent(request) && previous.Actor == actor
                ? Result<FirmProfessionalDutyDesignationView>.Success(previous.Response)
                : Failure(RequestErrorKind.Conflict,
                    "The request identity already retains another professional-duty intent or actor.")
            : null;

    static bool ValidAttribution(Uuid requestId, ActorReference actor, DateTimeOffset recordedAt) =>
        IndependenceRecordValidation.ValidAttribution(requestId, actor, recordedAt, "platform_operator");

    static bool IsSupportedDutyScope(string duty, Uuid? tenantId) => duty switch
    {
        FirmProfessionalDuty.EngagementPartner => tenantId is { } tenant && tenant != Uuid.Empty,
        FirmProfessionalDuty.RuleRatifier => tenantId is null,
        _ => false
    };

    static string Intent<T>(T value) => JsonSerializer.Serialize(value, value!.GetType(), ComplianceCoreJsonContext.Default);
    static bool Bounded(string value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximum && !value.Any(char.IsControl) && value == value.Trim();
    static Result<FirmProfessionalDutyDesignationView> Failure(RequestErrorKind kind, string message) =>
        Result<FirmProfessionalDutyDesignationView>.Failure(new RequestError(kind, message));
}
