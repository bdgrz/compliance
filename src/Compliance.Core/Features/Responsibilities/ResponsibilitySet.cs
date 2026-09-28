using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

/// <summary>Holds responsibility state for one exact source-record version inside its owner.</summary>
public sealed class ResponsibilitySet
{
    static readonly Uuid NamespaceId = Uuid.Parse("8a897fd3-a1cb-52d7-8f2b-651b3138756a",
        CultureInfo.InvariantCulture);

    readonly Uuid _tenantId;
    readonly ResponsibilityScope _scope;
    readonly Dictionary<Uuid, ResponsibilityAssignmentView> _assignments = [];

    public ResponsibilitySet(Uuid tenantId, ResponsibilityScope scope)
    {
        _tenantId = tenantId;
        _scope = scope;
    }

    public Uuid TenantId => _tenantId;

    public ResponsibilityScope Scope => _scope;

    public void Apply(ResponsibilityAssigned ev) =>
        _assignments[ev.AssignmentId] = new ResponsibilityAssignmentView(
            ev.TenantId, ev.AssignmentId, ev.MemberId, ev.Type, ev.Scope, ev.AssignedAt,
            ev.AssignedByMemberId, ev.EffectiveFrom, ev.EffectiveUntil, null, Uuid.Empty,
            ev.SeparationOfDutiesWaiverIds, ev.AssignedByDisplay);

    public void Apply(ResponsibilityRevoked ev)
    {
        if (_assignments.TryGetValue(ev.AssignmentId, out var assignment))
            _assignments[ev.AssignmentId] = assignment with
            {
                RevokedAt = ev.RevokedAt,
                RevokedByMemberId = ev.RevokedByMemberId,
                RevocationReason = ev.Reason,
                RevokedByDisplay = ev.RevokedByDisplay,
            };
    }

    public static Uuid IdFor(Uuid tenantId, ResponsibilityScope scope) =>
        Uuid.CreateVersion5(NamespaceId,
            $"{tenantId}\n{scope.RecordType}\n{scope.RecordId}\n{scope.VersionId}\n{scope.Revision}");

    public IReadOnlyList<ResponsibilityAssignmentView> ReadAssignments() => _assignments.Values
        .OrderBy(assignment => assignment.Type)
        .ThenBy(assignment => assignment.MemberId.ToString(), StringComparer.Ordinal)
        .ThenBy(assignment => assignment.AssignmentId.ToString(), StringComparer.Ordinal)
        .ToArray();

    public CommandFailure? Assign(Uuid assignmentId, Uuid memberId, ResponsibilityType type,
        Uuid assignedByMemberId, string assignedByDisplay, DateTimeOffset assignedAt, DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveUntil, IReadOnlyList<SeparationOfDutiesWaiver> waivers,
        Action<ResponsibilityAssigned>? raise = null)
    {
        ArgumentNullException.ThrowIfNull(waivers);
        if (_tenantId == Uuid.Empty || assignmentId == Uuid.Empty || memberId == Uuid.Empty ||
            assignedByMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(assignedByDisplay) ||
            !Enum.IsDefined(type) ||
            !IsValidScope(_scope) || effectiveUntil is { } until && until <= effectiveFrom)
            return CommandFailure.InvalidContent(
                "A responsibility requires a member, exact record version, valid interval, and assigning member.");

        var suppliedWaiverIds = waivers.Select(waiver => waiver.Id)
            .Distinct().OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();

        if (_assignments.TryGetValue(assignmentId, out var existing))
        {
            if (existing.RevokedAt is null && Equivalent(existing, memberId, type,
                    assignedByMemberId, assignedByDisplay, assignedAt, effectiveFrom,
                    effectiveUntil, suppliedWaiverIds))
                return null;
            return CommandFailure.StateConflict(
                "A responsibility assignment identifier cannot be reused with different or revoked terms.");
        }

        var proposed = new ResponsibilityAssignmentView(_tenantId, assignmentId, memberId,
            type, _scope, assignedAt, assignedByMemberId, effectiveFrom, effectiveUntil,
            null, Uuid.Empty, []);
        var conflicts = ResponsibilityConflictPolicy.FindConflicts(_assignments.Values, proposed);
        var acceptedWaivers = new HashSet<Uuid>();
        foreach (var conflict in conflicts)
        {
            var waiverScope = new SeparationOfDutiesWaiverScope(_scope.RecordType,
                _scope.RecordId, _scope.VersionId, _scope.Revision, conflict.WaiverAction);
            var waiver = waivers.FirstOrDefault(candidate => candidate.TenantId == _tenantId &&
                candidate.Allows(waiverScope, memberId, assignedAt));
            if (waiver is null)
                return CommandFailure.StateConflict(
                    "The responsibility conflicts with an existing assignment and requires an approved, active SoD waiver.");
            acceptedWaivers.Add(waiver.Id);
        }

        if (suppliedWaiverIds.Except(acceptedWaivers).Any())
            return CommandFailure.InvalidContent(
                "A responsibility assignment can reference only waivers that authorize its current conflicts.");

        var orderedWaivers = acceptedWaivers.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();
        ResponsibilityAssigned assigned = new(_tenantId, assignmentId, memberId, type,
            _scope, assignedAt, assignedByMemberId, effectiveFrom, effectiveUntil,
            orderedWaivers, assignedByDisplay.Trim())
        {
            StoredActor = ActorReference.ForMember(assignedByMemberId, assignedByDisplay.Trim()),
        };
        if (raise is null)
            Apply(assigned);
        else
            raise(assigned);
        return null;
    }

    public CommandFailure? Revoke(Uuid assignmentId, Uuid revokedByMemberId, string revokedByDisplay,
        DateTimeOffset revokedAt, string reason, Action<ResponsibilityRevoked>? raise = null)
    {
        if (revokedByMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(revokedByDisplay) ||
            string.IsNullOrWhiteSpace(reason))
            return CommandFailure.InvalidContent("A responsibility revocation requires an actor and reason.");
        if (!_assignments.TryGetValue(assignmentId, out var assignment))
            return CommandFailure.MissingRecord("The responsibility assignment was not found.");
        if (assignment.RevokedAt is { } prior)
            return prior == revokedAt && assignment.RevokedByMemberId == revokedByMemberId &&
                   StringComparer.Ordinal.Equals(assignment.RevocationReason, reason.Trim()) &&
                   StringComparer.Ordinal.Equals(assignment.RevokedByDisplay, revokedByDisplay.Trim())
                ? null
                : CommandFailure.StateConflict("The responsibility assignment has already been revoked.");
        ResponsibilityRevoked revoked = new(_tenantId, assignmentId, _scope, revokedByMemberId,
            revokedAt, reason.Trim(), revokedByDisplay.Trim())
        {
            StoredActor = ActorReference.ForMember(revokedByMemberId, revokedByDisplay.Trim()),
        };
        if (raise is null)
            Apply(revoked);
        else
            raise(revoked);
        return null;
    }

    static bool IsValidScope(ResponsibilityScope scope) =>
        !string.IsNullOrWhiteSpace(scope.RecordType) && scope.RecordId != Uuid.Empty &&
        scope.VersionId != Uuid.Empty && scope.Revision > 0;

    static bool Equivalent(ResponsibilityAssignmentView existing, Uuid memberId,
        ResponsibilityType type, Uuid assigningMemberId, string assigningDisplay,
        DateTimeOffset assignedAt, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveUntil,
        IReadOnlyList<Uuid> waiverIds) => existing.MemberId == memberId && existing.Type == type &&
        existing.AssignedByMemberId == assigningMemberId &&
        StringComparer.Ordinal.Equals(existing.AssignedByDisplay, assigningDisplay.Trim()) &&
        existing.AssignedAt == assignedAt && existing.EffectiveFrom == effectiveFrom &&
        existing.EffectiveUntil == effectiveUntil && existing.SeparationOfDutiesWaiverIds.SequenceEqual(waiverIds);
}
