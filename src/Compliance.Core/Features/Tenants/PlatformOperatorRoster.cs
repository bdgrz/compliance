using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
/// One platform stream owns the last-operator invariant. Tenant and member records remain separate.
/// </summary>
public sealed class PlatformOperatorRoster : Aggregate
{
    static readonly Uuid RosterId = Uuid.Parse(
        "bc279ad5-e9ae-5033-a174-4d1350ce2b07", CultureInfo.InvariantCulture);

    readonly HashSet<Uuid> _operators = [];
    bool _initialized;

    public PlatformOperatorRoster()
        : base(RosterId, new EventStreamAddress("bdgrz", "platform-operator-roster", RosterId.ToString()))
    {
        On<PlatformOperatorRosterSeeded>(Apply);
        On<PlatformOperatorGranted>(ev => _operators.Add(ev.SubjectUserId));
        On<PlatformOperatorRevoked>(ev => _operators.Remove(ev.SubjectUserId));
    }

    public bool IsInitialized => _initialized;
    public bool IsOperator(Uuid userId) => userId != Uuid.Empty && _operators.Contains(userId);
    public IReadOnlyList<Uuid> ActiveOperators => [.. _operators.OrderBy(static id => id.ToString(),
        StringComparer.Ordinal)];

    public Result Seed(IEnumerable<Uuid> configuredUserIds, DateTimeOffset occurredAt)
    {
        if (_initialized)
            return Result.Success;
        var userIds = configuredUserIds.Distinct()
            .OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray();
        if (userIds.Length == 0 || userIds.Contains(Uuid.Empty))
            return Failure(RequestErrorKind.Validation, "At least one valid bootstrap operator is required.");

        RaiseEvent(new PlatformOperatorRosterSeeded(userIds, occurredAt));
        return Result.Success;
    }

    public Result Grant(Uuid actorUserId, Uuid subjectUserId, string reason,
        DateTimeOffset occurredAt)
    {
        var validation = ValidateChange(actorUserId, subjectUserId, reason);
        if (validation is not null)
            return validation.Value;
        if (_operators.Contains(subjectUserId))
            return Result.Success;
        RaiseEvent(new PlatformOperatorGranted(actorUserId, subjectUserId, occurredAt, reason.Trim()));
        return Result.Success;
    }

    public Result Revoke(Uuid actorUserId, Uuid subjectUserId, string reason,
        DateTimeOffset occurredAt)
    {
        var validation = ValidateChange(actorUserId, subjectUserId, reason);
        if (validation is not null)
            return validation.Value;
        if (!_operators.Contains(subjectUserId))
            return Result.Success;
        if (_operators.Count == 1)
            return Failure(RequestErrorKind.Conflict, "The last platform operator cannot be revoked.");
        RaiseEvent(new PlatformOperatorRevoked(actorUserId, subjectUserId, occurredAt, reason.Trim()));
        return Result.Success;
    }

    Result? ValidateChange(Uuid actorUserId, Uuid subjectUserId, string reason)
    {
        if (!_initialized || !_operators.Contains(actorUserId))
            return Failure(RequestErrorKind.Forbidden, "The actor is not a platform operator.");
        if (subjectUserId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A platform user is required.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            return Failure(RequestErrorKind.Validation, "A reason of at most 500 characters is required.");
        return null;
    }

    void Apply(PlatformOperatorRosterSeeded seeded)
    {
        if (_initialized || seeded.UserIds.Length == 0 || seeded.UserIds.Contains(Uuid.Empty))
            throw new InvalidOperationException("The platform operator roster seed is invalid.");
        _initialized = true;
        _operators.UnionWith(seeded.UserIds);
    }

    static Result Failure(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
