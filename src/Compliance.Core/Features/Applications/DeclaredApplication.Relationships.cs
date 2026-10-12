using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class DeclaredApplication
{
    const string ActiveRelationshipState = "active";
    const string RemovedRelationshipState = "removed";

    readonly Dictionary<Uuid, ApplicationRelationshipState> _relationships = [];
    long _relationshipRevision;

    public long RelationshipRevision => _relationshipRevision;

    public IReadOnlyList<ApplicationRelationshipView> ReadApplicationRelationships() =>
        _relationships.Values.Where(static relationship => relationship.IsActive)
            .Select(static relationship => relationship.View)
            .OrderBy(static relationship => relationship.RelationshipId.ToString(),
                StringComparer.Ordinal)
            .ToArray();

    public bool TryGetApplicationRelationship(Uuid relationshipId,
        out ApplicationRelationshipView? relationship, out bool isActive)
    {
        if (_relationships.TryGetValue(relationshipId, out var state))
        {
            relationship = state.View;
            isActive = state.IsActive;
            return true;
        }
        relationship = null;
        isActive = false;
        return false;
    }

    public bool TryResolveApplicationRelationshipAdd(Uuid relationshipId,
        Uuid targetApplicationId, string relationshipType,
        out Result<ApplicationRelationshipChange> result)
    {
        if (!_relationships.TryGetValue(relationshipId, out var state))
        {
            result = default;
            return false;
        }
        result = state.View.TargetApplicationId == targetApplicationId &&
                 state.View.RelationshipType == relationshipType
            ? Result<ApplicationRelationshipChange>.Success(Change(state))
            : Conflict("A relationship ID cannot be reused with different content.");
        return true;
    }

    public bool TryResolveApplicationRelationshipRemoval(Uuid relationshipId,
        out Result<ApplicationRelationshipChange> result)
    {
        if (!_relationships.TryGetValue(relationshipId, out var state) || state.IsActive)
        {
            result = default;
            return false;
        }
        result = Result<ApplicationRelationshipChange>.Success(Change(state));
        return true;
    }

    public Result<ApplicationRelationshipChange> AddApplicationRelationship(
        long expectedRelationshipRevision, Uuid relationshipId, Uuid targetApplicationId,
        string relationshipType, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        if (relationshipId == Uuid.Empty || targetApplicationId == Uuid.Empty ||
            Id == Uuid.Empty || targetApplicationId == Id ||
            relationshipType is not "depends_on" and not "replaces")
            return Invalid("A relationship requires nonempty distinct IDs and a supported type.");
        if (expectedRelationshipRevision < 0)
            return Invalid("The expected relationship revision cannot be negative.");
        if (TryResolveApplicationRelationshipAdd(relationshipId, targetApplicationId,
                relationshipType, out var retry))
            return retry;
        if (!_created)
            return Missing("The application was not found.");
        if (_retired)
            return Conflict("Relationships cannot be changed on a retired application.");
        if (expectedRelationshipRevision != _relationshipRevision)
            return Result<ApplicationRelationshipChange>.Failure(
                CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                    "application relationship", _relationshipRevision)).ToRequestError());
        if (_relationships.Values.Any(relationship => relationship.IsActive &&
            relationship.View.TargetApplicationId == targetApplicationId &&
            relationship.View.RelationshipType == relationshipType))
            return Conflict("An active relationship of this type already exists for the target.");

        var nextRevision = _relationshipRevision + 1;
        RaiseEvent(new ApplicationRelationshipAdded(_tenantId, Id, relationshipId,
            nextRevision, targetApplicationId, relationshipType, actorMemberId,
            actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return Result<ApplicationRelationshipChange>.Success(new ApplicationRelationshipChange(
            relationshipId, nextRevision, ActiveRelationshipState));
    }

    public Result<ApplicationRelationshipChange> RemoveApplicationRelationship(
        long expectedRelationshipRevision, Uuid relationshipId, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        if (relationshipId == Uuid.Empty)
            return Invalid("A relationship ID cannot be empty.");
        if (!_relationships.TryGetValue(relationshipId, out var state))
            return Missing("The application relationship was not found.");
        if (!state.IsActive)
            return Result<ApplicationRelationshipChange>.Success(Change(state));
        if (!_created)
            return Missing("The application was not found.");
        if (_retired)
            return Conflict("Relationships cannot be changed on a retired application.");
        if (expectedRelationshipRevision < 0)
            return Invalid("The expected relationship revision cannot be negative.");
        if (expectedRelationshipRevision != _relationshipRevision)
            return Result<ApplicationRelationshipChange>.Failure(
                CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                    "application relationship", _relationshipRevision)).ToRequestError());

        var nextRevision = _relationshipRevision + 1;
        RaiseEvent(new ApplicationRelationshipRemoved(_tenantId, Id, relationshipId,
            nextRevision, state.View.TargetApplicationId, state.View.RelationshipType,
            actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return Result<ApplicationRelationshipChange>.Success(new ApplicationRelationshipChange(
            relationshipId, nextRevision, RemovedRelationshipState));
    }

    void RegisterApplicationRelationshipEvents()
    {
        On<ApplicationRelationshipAdded>(ev =>
        {
            if (ev.TenantId != _tenantId || ev.ApplicationId != Id ||
                ev.RelationshipId == Uuid.Empty || ev.TargetApplicationId == Uuid.Empty ||
                ev.TargetApplicationId == Id || ev.RelationshipType is not "depends_on" and not "replaces" ||
                ev.RelationshipRevision != _relationshipRevision + 1 ||
                _relationships.ContainsKey(ev.RelationshipId) ||
                _relationships.Values.Any(relationship => relationship.IsActive &&
                    relationship.View.TargetApplicationId == ev.TargetApplicationId &&
                    relationship.View.RelationshipType == ev.RelationshipType))
                throw new InvalidOperationException("The application relationship event is invalid.");
            _relationshipRevision = ev.RelationshipRevision;
            _relationships.Add(ev.RelationshipId, new ApplicationRelationshipState(
                new ApplicationRelationshipView(ev.RelationshipId, ev.TargetApplicationId,
                    ev.RelationshipType, ev.ActorMemberId, ev.ActorDisplay, ev.ChangedAt), true,
                ev.RelationshipRevision));
        });
        On<ApplicationRelationshipRemoved>(ev =>
        {
            if (ev.TenantId != _tenantId || ev.ApplicationId != Id ||
                ev.RelationshipRevision != _relationshipRevision + 1 ||
                !_relationships.TryGetValue(ev.RelationshipId, out var state) ||
                !state.IsActive || state.View.TargetApplicationId != ev.TargetApplicationId ||
                state.View.RelationshipType != ev.RelationshipType)
                throw new InvalidOperationException("The application relationship removal event is invalid.");
            _relationshipRevision = ev.RelationshipRevision;
            _relationships[ev.RelationshipId] = state with
            {
                IsActive = false,
                RelationshipRevision = ev.RelationshipRevision,
            };
        });
    }

    static ApplicationRelationshipChange Change(ApplicationRelationshipState state) =>
        new(state.View.RelationshipId, state.RelationshipRevision,
            state.IsActive ? ActiveRelationshipState : RemovedRelationshipState);

    static Result<ApplicationRelationshipChange> Invalid(string message) =>
        Result<ApplicationRelationshipChange>.Failure(new RequestError(RequestErrorKind.Validation,
            message));

    static Result<ApplicationRelationshipChange> Missing(string message) =>
        Result<ApplicationRelationshipChange>.Failure(new RequestError(RequestErrorKind.NotFound,
            message));

    static Result<ApplicationRelationshipChange> Conflict(string message) =>
        Result<ApplicationRelationshipChange>.Failure(new RequestError(RequestErrorKind.Conflict,
            message));

    sealed record ApplicationRelationshipState(ApplicationRelationshipView View,
        bool IsActive, long RelationshipRevision);
}
