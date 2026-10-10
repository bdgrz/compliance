using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class FitzApplicationRelationshipDirectory(IKvClient client)
    : FitzKvProjectionStore(client,
            "kv://bdgrz/application-relationships-v1/projection",
            "ApplicationRelationshipsV1"),
        IApplicationRelationshipDirectory, IApplicationRelationshipProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ApplicationRelationshipRecorded recorded:
                await ApplyRecordedAsync(recorded, ct).ConfigureAwait(false);
                break;
            case ApplicationRelationshipRemoved removed:
                await ApplyRemovedAsync(removed, ct).ConfigureAwait(false);
                break;
            case ApplicationRelationshipApproved approved:
                await ApplyApprovedAsync(approved, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<Page<ApplicationRelationshipView>> ListAsync(Uuid tenantId,
        Uuid applicationId, string direction, int limit, string? cursor,
        CancellationToken ct = default)
    {
        if (direction is not ("outgoing" or "incoming"))
            throw new ArgumentOutOfRangeException(nameof(direction));
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var query = direction == "outgoing"
            ? ApplicationRelationshipSchema.BySourceApplication.Query()
                .WithPrefix(applicationId.ToString())
            : ApplicationRelationshipSchema.ByTargetApplication.Query()
                .WithPrefix(applicationId.ToString());
        return await ApplicationRelationshipSchema.Relationships.QueryAsync(tx,
            query.Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("ApplicationRelationshipsV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "applications")), ct);

    async ValueTask ApplyRecordedAsync(ApplicationRelationshipRecorded ev,
        CancellationToken ct)
    {
        ValidateScope(ev.TenantId, ev.RelationshipId, ev.SourceApplicationId,
            ev.TargetApplicationId, ev.RelationshipType);
        var expectedStatus = ev.RelationshipType == ApplicationRelationshipIdentity.Replaces
            ? "proposed"
            : "active";
        if (ev.Revision < 1 || ev.SourceApplicationRevision < 1 ||
            ev.TargetApplicationRevision < 1 || ev.Status != expectedStatus ||
            ev.ActorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.ActorDisplay) ||
            ev.ChangedAt == default)
            throw new InvalidOperationException("An application relationship record is invalid.");

        var current = await ApplicationRelationshipSchema.Relationships
            .GetAsync(Transaction, ev.RelationshipId, ct).ConfigureAwait(false);
        var expected = new ApplicationRelationshipView(ev.TenantId, ev.RelationshipId,
            ev.SourceApplicationId, ev.TargetApplicationId, ev.RelationshipType,
            ev.Revision, ev.Status, ev.Actor, ev.ChangedAt,
            ev.SourceApplicationRevision, ev.TargetApplicationRevision);
        if (current == expected)
            return;
        if (current is null)
        {
            if (ev.Revision != 1)
                throw new InvalidOperationException("An application relationship record is out of order.");
            await ApplicationRelationshipSchema.Relationships.InsertAsync(Transaction,
                expected, ct).ConfigureAwait(false);
            return;
        }
        if (current.TenantId != ev.TenantId || current.SourceApplicationId != ev.SourceApplicationId ||
            current.TargetApplicationId != ev.TargetApplicationId ||
            current.RelationshipType != ev.RelationshipType || current.Status != "removed" ||
            ev.Revision != current.Revision + 1)
            throw new InvalidOperationException("An application relationship record is out of order.");
        await ApplicationRelationshipSchema.Relationships.ReplaceAsync(Transaction, current,
            expected, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyRemovedAsync(ApplicationRelationshipRemoved ev,
        CancellationToken ct)
    {
        ValidateScope(ev.TenantId, ev.RelationshipId, ev.SourceApplicationId,
            ev.TargetApplicationId, ev.RelationshipType);
        if (ev.Revision < 2 || string.IsNullOrWhiteSpace(ev.Reason) || ev.Reason.Length > 2000 ||
            ev.ActorMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.ActorDisplay) ||
            ev.ChangedAt == default)
            throw new InvalidOperationException("An application relationship removal is invalid.");
        var current = await RequireCurrentAsync(ev.RelationshipId, ct).ConfigureAwait(false);
        var expected = current with
        {
            Revision = ev.Revision,
            Status = "removed",
            ChangedBy = ev.Actor,
            ChangedAt = ev.ChangedAt,
            RemovalReason = ev.Reason,
            Approval = null,
        };
        if (current.Revision == ev.Revision && current == expected)
            return;
        if (current.TenantId != ev.TenantId || current.SourceApplicationId != ev.SourceApplicationId ||
            current.TargetApplicationId != ev.TargetApplicationId ||
            current.RelationshipType != ev.RelationshipType || current.Status == "removed" ||
            ev.Revision != current.Revision + 1)
            throw new InvalidOperationException("An application relationship removal is out of order.");
        await ApplicationRelationshipSchema.Relationships.ReplaceAsync(Transaction, current,
            expected, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyApprovedAsync(ApplicationRelationshipApproved ev,
        CancellationToken ct)
    {
        ValidateScope(ev.TenantId, ev.RelationshipId, ev.SourceApplicationId,
            ev.TargetApplicationId, ApplicationRelationshipIdentity.Replaces);
        if (ev.Revision < 2 || ev.SourceApplicationRevision < 1 ||
            ev.TargetApplicationRevision < 1 ||
            !ApplicationRelationshipIdentity.IsImpactDigest(ev.ImpactDigest) ||
            ev.ApproverMemberId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.ApproverDisplay) ||
            ev.ApprovedAt == default)
            throw new InvalidOperationException("An application relationship approval is invalid.");
        var current = await RequireCurrentAsync(ev.RelationshipId, ct).ConfigureAwait(false);
        var expected = current with
        {
            Revision = ev.Revision,
            Status = "approved",
            ChangedBy = ev.Approver,
            ChangedAt = ev.ApprovedAt,
            SourceApplicationRevision = ev.SourceApplicationRevision,
            TargetApplicationRevision = ev.TargetApplicationRevision,
            RemovalReason = null,
            Approval = new ApplicationRelationshipApproval(ev.ApproverMemberId,
                ev.Approver, ev.SourceApplicationRevision, ev.TargetApplicationRevision,
                ev.ImpactDigest, ev.ApprovedAt),
        };
        if (current.Revision == ev.Revision && current == expected)
            return;
        if (current.TenantId != ev.TenantId || current.SourceApplicationId != ev.SourceApplicationId ||
            current.TargetApplicationId != ev.TargetApplicationId ||
            current.RelationshipType != ApplicationRelationshipIdentity.Replaces ||
            current.Status != "proposed" || ev.Revision != current.Revision + 1 ||
            current.SourceApplicationRevision != ev.SourceApplicationRevision ||
            current.TargetApplicationRevision != ev.TargetApplicationRevision)
            throw new InvalidOperationException("An application relationship approval is out of order.");
        await ApplicationRelationshipSchema.Relationships.ReplaceAsync(Transaction, current,
            expected, ct).ConfigureAwait(false);
    }

    async ValueTask<ApplicationRelationshipView> RequireCurrentAsync(Uuid relationshipId,
        CancellationToken ct) => await ApplicationRelationshipSchema.Relationships
        .GetAsync(Transaction, relationshipId, ct).ConfigureAwait(false) ??
        throw new InvalidOperationException("An application relationship change cannot project before its record.");

    static void ValidateScope(Uuid tenantId, Uuid relationshipId, Uuid sourceApplicationId,
        Uuid targetApplicationId, string relationshipType)
    {
        if (tenantId == Uuid.Empty || relationshipId == Uuid.Empty ||
            sourceApplicationId == Uuid.Empty || targetApplicationId == Uuid.Empty ||
            sourceApplicationId == targetApplicationId ||
            !ApplicationRelationshipIdentity.IsSupported(relationshipType) ||
            relationshipId != ApplicationRelationshipIdentity.Id(sourceApplicationId,
                relationshipType, targetApplicationId))
            throw new InvalidOperationException("An application relationship scope is invalid.");
    }
}
