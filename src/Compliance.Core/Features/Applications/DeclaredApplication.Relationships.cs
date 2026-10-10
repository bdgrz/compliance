using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class DeclaredApplication
{
    readonly Dictionary<Uuid, ApplicationRelationshipView> _relationships = [];

    public IReadOnlyList<ApplicationRelationshipView> Relationships =>
        Array.AsReadOnly(_relationships.Values.OrderBy(static item => item.RelationshipId.ToString(),
            StringComparer.Ordinal).ToArray());

    void RegisterRelationshipEvents()
    {
        On<ApplicationRelationshipRecorded>(ApplyRelationshipRecorded);
        On<ApplicationRelationshipApproved>(ApplyRelationshipApproved);
        On<ApplicationRelationshipRemoved>(ApplyRelationshipRemoved);
    }

    public ApplicationRelationshipView? Relationship(string relationshipType,
        Uuid targetApplicationId) => _relationships.GetValueOrDefault(
        ApplicationRelationshipIdentity.Id(Id, relationshipType, targetApplicationId));

    public Result<ApplicationRelationshipRegistration> RecordRelationship(
        Uuid targetApplicationId, long sourceApplicationRevision,
        long targetApplicationRevision, string relationshipType, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created || _retired)
            return Result<ApplicationRelationshipRegistration>.Failure(new RequestError(
                RequestErrorKind.Conflict, "Relationships require an active source application."));
        if (targetApplicationId == Uuid.Empty || targetApplicationId == Id ||
            sourceApplicationRevision != _revision || targetApplicationRevision < 1 ||
            !ApplicationRelationshipIdentity.IsSupported(relationshipType) ||
            actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(actorDisplay) ||
            changedAt == default)
            return Result<ApplicationRelationshipRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, "The application relationship is invalid."));

        var relationshipId = ApplicationRelationshipIdentity.Id(Id, relationshipType,
            targetApplicationId);
        var current = _relationships.GetValueOrDefault(relationshipId);
        if (current is { Status: "active" or "proposed" or "approved" })
        {
            if (current.SourceApplicationRevision != sourceApplicationRevision ||
                current.TargetApplicationRevision != targetApplicationRevision)
                return Result<ApplicationRelationshipRegistration>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The existing relationship was recorded against different application revisions."));
            return Result<ApplicationRelationshipRegistration>.Success(
                new ApplicationRelationshipRegistration(current.RelationshipId,
                    current.Revision, current.Status));
        }

        var revision = (current?.Revision ?? 0) + 1;
        var status = relationshipType == ApplicationRelationshipIdentity.Replaces
            ? "proposed"
            : "active";
        RaiseEvent(new ApplicationRelationshipRecorded(_tenantId, relationshipId, Id,
            targetApplicationId, relationshipType, revision, status, _revision,
            targetApplicationRevision, actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return Result<ApplicationRelationshipRegistration>.Success(
            new ApplicationRelationshipRegistration(relationshipId, revision, status));
    }

    public CommandFailure? RemoveRelationship(Uuid targetApplicationId,
        long expectedRelationshipRevision, string relationshipType, string reason,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The source application was not found.");
        if (!ApplicationRelationshipIdentity.IsSupported(relationshipType) ||
            targetApplicationId == Uuid.Empty || targetApplicationId == Id ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > 2000 ||
            actorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(actorDisplay) ||
            changedAt == default || expectedRelationshipRevision < 1)
            return CommandFailure.InvalidContent("The application relationship removal is invalid.");
        var relationshipId = ApplicationRelationshipIdentity.Id(Id, relationshipType,
            targetApplicationId);
        var current = _relationships.GetValueOrDefault(relationshipId);
        if (current is null || current.Status == "removed")
            return null;
        if (expectedRelationshipRevision != current.Revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "application relationship", current.Revision));

        RaiseEvent(new ApplicationRelationshipRemoved(_tenantId, relationshipId, Id,
            targetApplicationId, relationshipType, current.Revision + 1, reason.Trim(),
            actorMemberId, actorDisplay, changedAt)
        {
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        });
        return null;
    }

    public CommandFailure? ApproveSuccessor(Uuid targetApplicationId,
        long expectedSourceRevision, long expectedRelationshipRevision,
        long expectedTargetRevision, string impactDigest, Uuid approverMemberId,
        string approverDisplay, DateTimeOffset approvedAt)
    {
        if (!_created || _retired)
            return CommandFailure.MissingRecord("The predecessor application was not found.");
        if (expectedSourceRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision(
                "application", _revision));
        var relationship = Relationship(ApplicationRelationshipIdentity.Replaces,
            targetApplicationId);
        if (relationship is not { Status: "proposed" } ||
            relationship.Revision != expectedRelationshipRevision ||
            relationship.SourceApplicationRevision != expectedSourceRevision ||
            expectedTargetRevision < 1 ||
            relationship.TargetApplicationRevision != expectedTargetRevision ||
            !ApplicationRelationshipIdentity.IsImpactDigest(impactDigest) ||
            approverMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(approverDisplay) || approvedAt == default)
            return CommandFailure.StateConflict(
                "The proposed successor or impact assessment changed. Reload before approval.");
        if (relationship.ChangedBy.Id == approverMemberId.ToString())
            return CommandFailure.StateConflict(
                "A successor must be approved by an active member other than its proposer.");

        RaiseEvent(new ApplicationRelationshipApproved(_tenantId,
            relationship.RelationshipId, Id, targetApplicationId, relationship.Revision + 1,
            _revision, expectedTargetRevision, impactDigest, approverMemberId,
            approverDisplay, approvedAt)
        {
            StoredApprover = ActorReference.ForMember(approverMemberId, approverDisplay),
        });
        return null;
    }

    void ApplyRelationshipRecorded(ApplicationRelationshipRecorded ev)
    {
        if (ev.TenantId != _tenantId || ev.SourceApplicationId != Id ||
            ev.RelationshipId != ApplicationRelationshipIdentity.Id(Id, ev.RelationshipType,
                ev.TargetApplicationId) || ev.Revision < 1 ||
            ev.SourceApplicationRevision != _revision ||
            ev.TargetApplicationRevision < 1 || !ApplicationRelationshipIdentity.IsSupported(ev.RelationshipType) ||
            ev.Status != (ev.RelationshipType == ApplicationRelationshipIdentity.Replaces
                ? "proposed" : "active") || ev.ActorMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(ev.ActorDisplay) || ev.ChangedAt == default)
            throw new InvalidOperationException("An application relationship event is invalid.");
        var current = _relationships.GetValueOrDefault(ev.RelationshipId);
        if (ev.Revision != (current?.Revision ?? 0) + 1 || current is { Status: not "removed" })
            throw new InvalidOperationException("An application relationship event is out of order.");
        _relationships[ev.RelationshipId] = new ApplicationRelationshipView(_tenantId,
            ev.RelationshipId, Id, ev.TargetApplicationId, ev.RelationshipType, ev.Revision,
            ev.Status, ev.Actor, ev.ChangedAt, ev.SourceApplicationRevision,
            ev.TargetApplicationRevision);
    }

    void ApplyRelationshipApproved(ApplicationRelationshipApproved ev)
    {
        var current = _relationships.GetValueOrDefault(ev.RelationshipId);
        if (ev.TenantId != _tenantId || ev.SourceApplicationId != Id ||
            current is not { Status: "proposed" } || current.Revision + 1 != ev.Revision ||
            current.TargetApplicationId != ev.TargetApplicationId ||
            ev.SourceApplicationRevision != _revision ||
            ev.SourceApplicationRevision != current.SourceApplicationRevision ||
            ev.TargetApplicationRevision != current.TargetApplicationRevision ||
            !ApplicationRelationshipIdentity.IsImpactDigest(ev.ImpactDigest) ||
            ev.ApproverMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.ApproverDisplay) ||
            ev.ApprovedAt == default ||
            current.ChangedBy.Id == ev.ApproverMemberId.ToString())
            throw new InvalidOperationException("An application successor approval is invalid.");
        _relationships[ev.RelationshipId] = current with
        {
            Revision = ev.Revision,
            Status = "approved",
            ChangedBy = ev.Approver,
            ChangedAt = ev.ApprovedAt,
            Approval = new ApplicationRelationshipApproval(ev.ApproverMemberId,
                ev.Approver, ev.SourceApplicationRevision, ev.TargetApplicationRevision,
                ev.ImpactDigest, ev.ApprovedAt),
        };
    }

    void ApplyRelationshipRemoved(ApplicationRelationshipRemoved ev)
    {
        var current = _relationships.GetValueOrDefault(ev.RelationshipId);
        if (ev.TenantId != _tenantId || ev.SourceApplicationId != Id ||
            current is null || current.Status == "removed" || current.Revision + 1 != ev.Revision ||
            current.TargetApplicationId != ev.TargetApplicationId ||
            current.RelationshipType != ev.RelationshipType || string.IsNullOrWhiteSpace(ev.Reason) ||
            ev.Reason.Length > 2000 || ev.ActorMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(ev.ActorDisplay) || ev.ChangedAt == default)
            throw new InvalidOperationException("An application relationship removal is invalid.");
        _relationships[ev.RelationshipId] = current with
        {
            Revision = ev.Revision,
            Status = "removed",
            ChangedBy = ev.Actor,
            ChangedAt = ev.ChangedAt,
            RemovalReason = ev.Reason,
        };
    }
}

static class ApplicationRelationshipIdentity
{
    public const string DependsOn = "depends_on";
    public const string Replaces = "replaces";

    public static bool IsImpactDigest(string? digest) => digest is { Length: 64 } &&
        digest.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    public static bool IsSupported(string relationshipType) =>
        relationshipType is DependsOn or Replaces;

    public static Uuid Id(Uuid sourceApplicationId, string relationshipType,
        Uuid targetApplicationId) => Uuid.CreateVersion5(sourceApplicationId,
        $"application-relationship:{relationshipType}:{targetApplicationId}");
}
