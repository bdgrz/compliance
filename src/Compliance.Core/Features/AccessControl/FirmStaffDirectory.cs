using System.Globalization;
using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Platform metadata only: canonical staff identity/practice; never client grants or partner authority.</summary>
public sealed class FirmStaffDirectory : Aggregate
{
    static readonly Uuid DirectoryId = Uuid.Parse("fd4a1b22-1eb4-54f2-9029-8fd722e61e8c", CultureInfo.InvariantCulture);
    readonly Dictionary<Uuid, FirmStaffMemberView> _staff = [];
    readonly Dictionary<Uuid, (string Intent, ActorReference Actor, FirmStaffMemberView Response)> _decisions = [];

    public FirmStaffDirectory() : base(DirectoryId,
        new EventStreamAddress("bdgrz", "firm-staff-directory", DirectoryId.ToString()))
    {
        On<FirmStaffChangeRecorded>(Apply);
    }

    public long Sequence { get; private set; }
    public FirmStaffMemberView? Get(Uuid staffMemberId) => _staff.GetValueOrDefault(staffMemberId);

    /// <summary>Verifies historical decision facts; trusted delivery must separately validate the source stream.</summary>
    internal bool MatchesRetainedStatus(FirmStaffChangeRecorded change) =>
        change.Operation == "status" && change.Staff is not null && Bounded(change.Reason!, 2000) &&
        _decisions.TryGetValue(change.RequestId, out var previous) &&
        previous.Actor == change.Staff.Actor && previous.Response == change.Staff &&
        previous.Intent == Intent(new SetFirmStaffStatus(change.Staff.StaffMemberId,
            change.Staff.IsActive, change.Reason!, change.ExpectedSequence));
    public FirmStaffDirectoryView View(bool activeOnly = false) => new(Sequence,
        Array.AsReadOnly(_staff.Values.Where(staff => !activeOnly || staff.IsActive)
            .OrderBy(staff => staff.StaffMemberId.ToString(), StringComparer.Ordinal).ToArray()));

    public Result<FirmStaffMemberView> Register(Uuid requestId, Uuid staffMemberId, Uuid userId,
        string practice, string sourceReference, long expectedSequence, ActorReference actor,
        DateTimeOffset recordedAt)
    {
        if (!IndependenceRecordValidation.ValidAttribution(requestId, actor, recordedAt, "platform_operator") ||
            staffMemberId == Uuid.Empty || userId == Uuid.Empty || practice is not ("advisory" or "attest") ||
            !Bounded(sourceReference, 2000))
            return Failure(RequestErrorKind.Validation, "Directory registration requires a canonical user, practice and attributed general staff source.");
        var request = new RegisterFirmStaff(staffMemberId, userId, practice, sourceReference, expectedSequence);
        if (Retry(requestId, request, actor) is { } retry)
            return retry;
        if (expectedSequence != Sequence || _staff.Count >= 1000 || _staff.ContainsKey(staffMemberId) ||
            _staff.Values.Any(staff => staff.UserId == userId))
            return Failure(RequestErrorKind.Conflict, "Reload the directory; staff and canonical user identities are immutable and unique.");
        var staff = new FirmStaffMemberView(staffMemberId, userId, practice, sourceReference, true, 1, actor, recordedAt);
        RaiseEvent(new FirmStaffChangeRecorded(requestId, expectedSequence, "register", null, staff));
        return Result<FirmStaffMemberView>.Success(staff);
    }

    public Result<FirmStaffMemberView> SetStatus(Uuid requestId, Uuid staffMemberId, bool isActive,
        string reason, long expectedSequence, ActorReference actor, DateTimeOffset recordedAt)
    {
        if (!IndependenceRecordValidation.ValidAttribution(requestId, actor, recordedAt, "platform_operator") ||
            staffMemberId == Uuid.Empty || !Bounded(reason, 2000))
            return Failure(RequestErrorKind.Validation, "A staff status change requires an attributed operator and bounded reason.");
        var request = new SetFirmStaffStatus(staffMemberId, isActive, reason, expectedSequence);
        if (Retry(requestId, request, actor) is { } retry)
            return retry;
        if (expectedSequence != Sequence)
            return Failure(RequestErrorKind.Conflict, "Reload the directory before changing staff status.");
        if (Get(staffMemberId) is not { } current)
            return Failure(RequestErrorKind.NotFound, "The staff member was not found.");
        var staff = current with { IsActive = isActive, Revision = current.Revision + 1, Actor = actor, RecordedAt = recordedAt };
        RaiseEvent(new FirmStaffChangeRecorded(requestId, expectedSequence, "status", reason, staff));
        return Result<FirmStaffMemberView>.Success(staff);
    }

    void Apply(FirmStaffChangeRecorded change)
    {
        var current = Get(change.Staff.StaffMemberId);
        if (change.Staff.StaffMemberId == Uuid.Empty || change.Staff.UserId == Uuid.Empty ||
            change.Staff.Practice is not ("advisory" or "attest") || !Bounded(change.Staff.SourceReference, 2000) ||
            !IndependenceRecordValidation.ValidAttribution(change.RequestId, change.Staff.Actor,
                change.Staff.RecordedAt, "platform_operator") ||
            change.ExpectedSequence != Sequence || change.Staff.Revision != (current?.Revision ?? 0) + 1 ||
            current is not null && (current.UserId != change.Staff.UserId || current.Practice != change.Staff.Practice ||
                current.SourceReference != change.Staff.SourceReference) ||
            current is null && _staff.Values.Any(staff => staff.UserId == change.Staff.UserId))
            throw new InvalidOperationException("Staff records must preserve their canonical identity, practice and predecessor.");
        object request = change.Operation switch
        {
            "register" when current is null => new RegisterFirmStaff(change.Staff.StaffMemberId,
                change.Staff.UserId, change.Staff.Practice, change.Staff.SourceReference, change.ExpectedSequence),
            "status" when current is not null && Bounded(change.Reason!, 2000) => new SetFirmStaffStatus(change.Staff.StaffMemberId,
                change.Staff.IsActive, change.Reason!, change.ExpectedSequence),
            _ => throw new InvalidOperationException("A directory event must be a registration or an existing staff status change."),
        };
        _staff[change.Staff.StaffMemberId] = change.Staff;
        _decisions.Add(change.RequestId, (Intent(request), change.Staff.Actor, change.Staff));
        Sequence++;
    }

    Result<FirmStaffMemberView>? Retry(Uuid requestId, object request, ActorReference actor) =>
        _decisions.TryGetValue(requestId, out var previous)
            ? previous.Intent == Intent(request) && previous.Actor == actor
                ? Result<FirmStaffMemberView>.Success(previous.Response)
                : Failure(RequestErrorKind.Conflict, "The request identity already retains another directory intent or actor.")
            : null;

    static string Intent<T>(T value) => JsonSerializer.Serialize(value, value!.GetType(), ComplianceCoreJsonContext.Default);
    static bool Bounded(string value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximum && !value.Any(char.IsControl) && value == value.Trim();
    static Result<FirmStaffMemberView> Failure(RequestErrorKind kind, string message) =>
        Result<FirmStaffMemberView>.Failure(new RequestError(kind, message));
}
