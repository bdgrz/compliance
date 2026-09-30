using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Observes joiners, movers, and leavers by comparing each accepted roster version with the
///     previous one (M0-D06). Observations are compliance work only: nothing here grants or revokes
///     platform access. Manager changes are restricted and never produce list-visible observations.
/// </summary>
sealed class FitzWorkforceObservationDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/workforce-observations-v1/projection",
            "WorkforceObservationsV1"),
        IWorkforceObservationDirectoryReader, IWorkforceObservationProjection
{
    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("WorkforceObservationsV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "work-relationships")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case WorkRelationshipRecorded recorded:
                var initial = new RosterRelationshipSnapshot(recorded.TenantId,
                    recorded.RelationshipId, recorded.PersonId, recorded.SourceWorkerId, 1,
                    recorded.Terms);
                await WorkforceObservationSchema.Relationships.InsertAsync(Transaction, initial, ct)
                    .ConfigureAwait(false);
                var ended = recorded.Terms.LifecycleStatus == "ended";
                await ObserveAsync(initial, ended ? "leaver" : "joiner",
                    ended ? recorded.Terms.EndDate!.Value : recorded.Terms.StartDate, [],
                    recorded.Actor, recorded.ChangedAt, ct).ConfigureAwait(false);
                break;
            case WorkRelationshipRevised revised:
                var current = await WorkforceObservationSchema.Relationships.GetAsync(Transaction,
                    revised.RelationshipId, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != revised.TenantId ||
                    current.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A work relationship revision cannot be observed before its predecessor.");
                var next = current with { Revision = revised.Revision, Terms = revised.Terms };
                await WorkforceObservationSchema.Relationships.ReplaceAsync(Transaction, current,
                    next, ct).ConfigureAwait(false);
                var wasEnded = current.Terms.LifecycleStatus == "ended";
                var isEnded = revised.Terms.LifecycleStatus == "ended";
                if (!wasEnded && isEnded)
                    await ObserveAsync(next, "leaver", revised.Terms.EndDate!.Value, [],
                        revised.Actor, revised.ChangedAt, ct).ConfigureAwait(false);
                else if (wasEnded && !isEnded)
                    await ObserveAsync(next, "joiner", revised.Terms.StartDate, [],
                        revised.Actor, revised.ChangedAt, ct).ConfigureAwait(false);
                else if (!isEnded && MovedFields(current.Terms, revised.Terms) is { Count: > 0 } moved)
                    await ObserveAsync(next, "mover", DateOnly.FromDateTime(
                            revised.ChangedAt.UtcDateTime), moved, revised.Actor,
                        revised.ChangedAt, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<Page<WorkforceObservationView>> ListAsync(Uuid tenantId, string? kind,
        int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var take = Math.Clamp(limit, 1, 200);
        return kind is null
            ? await WorkforceObservationSchema.Observations.QueryAsync(tx,
                WorkforceObservationSchema.ByObservedAt.Query().Take(take).After(cursor), ct)
                .ConfigureAwait(false)
            : await WorkforceObservationSchema.Observations.QueryAsync(tx,
                WorkforceObservationSchema.ByKind.Query().WithPrefix(kind).Take(take)
                    .After(cursor), ct).ConfigureAwait(false);
    }

    public async ValueTask<WorkforceObservationView?> GetAsync(Uuid tenantId, Uuid observationId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var view = await WorkforceObservationSchema.Observations.GetAsync(tx, observationId, ct)
            .ConfigureAwait(false);
        return view?.TenantId == tenantId ? view : null;
    }

    public async ValueTask<IReadOnlyList<string>> ListRelationshipStatusesAsync(Uuid tenantId,
        Uuid personId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var statuses = new List<string>();
        string? cursor = null;
        do
        {
            var page = await WorkforceObservationSchema.Relationships.QueryAsync(tx,
                WorkforceObservationSchema.ByPerson.Query().WithPrefix(personId.ToString())
                    .Take(200).After(cursor), ct).ConfigureAwait(false);
            statuses.AddRange(page.Items.Where(item => item.TenantId == tenantId)
                .Select(static item => item.Terms.LifecycleStatus));
            cursor = page.NextCursor;
        } while (cursor is not null);
        return statuses;
    }

    static List<string> MovedFields(WorkRelationshipTerms before, WorkRelationshipTerms after)
    {
        var fields = new List<string>();
        if (before.WorkerType != after.WorkerType)
            fields.Add("worker_type");
        if (before.Department != after.Department)
            fields.Add("department");
        if (before.SponsorPersonId != after.SponsorPersonId)
            fields.Add("sponsor");
        return fields;
    }

    ValueTask ObserveAsync(RosterRelationshipSnapshot relationship, string kind,
        DateOnly effectiveDate, IReadOnlyList<string> changedFields, ActorReference actor,
        DateTimeOffset observedAt, CancellationToken ct) =>
        WorkforceObservationSchema.Observations.InsertAsync(Transaction,
            new WorkforceObservationView(relationship.TenantId,
                Uuid.CreateVersion5(relationship.RelationshipId,
                    $"observation:{relationship.Revision}"), kind, "open",
                relationship.RelationshipId, relationship.PersonId, relationship.SourceWorkerId,
                relationship.Revision, effectiveDate, changedFields, actor, observedAt), ct);
}
