using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Reads the caught-up roster and tenant memberships and evaluates reconciliation observations.
///     The roster is bounded by the same inline limit as a roster snapshot.
/// </summary>
public sealed class WorkforceRosterReconciler(IPersonDirectoryReader people,
    PersonReadConsistency peopleConsistency, IWorkRelationshipDirectoryReader relationships,
    WorkRelationshipReadConsistency relationshipConsistency,
    ITenantMembershipDirectoryReader memberships, TimeProvider clock)
{
    public async ValueTask<Result<IReadOnlyList<WorkforceReconciliationObservationView>>>
        EvaluateAsync(Uuid tenantId, CancellationToken ct)
    {
        var peopleReady = await peopleConsistency.EnsureListCaughtUpAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (!peopleReady.IsSuccess)
            return Failure(peopleReady.Error);
        var relationshipsReady = await relationshipConsistency.EnsureListCaughtUpAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (!relationshipsReady.IsSuccess)
            return Failure(relationshipsReady.Error);
        var roster = await ReadAllAsync((cursor, token) =>
            people.ListAsync(tenantId, 200, cursor, token), ct).ConfigureAwait(false);
        var jobs = await ReadAllAsync((cursor, token) =>
            relationships.ListAsync(tenantId, 200, cursor, token), ct).ConfigureAwait(false);
        var members = await ReadAllAsync((cursor, token) =>
            memberships.ListAsync(tenantId, 200, cursor, token), ct).ConfigureAwait(false);
        if (roster is null || jobs is null || members is null)
            return Failure(new RequestError(RequestErrorKind.Validation,
                $"Roster reconciliation currently supports at most {MaximumRows} rows."));
        return Result<IReadOnlyList<WorkforceReconciliationObservationView>>.Success(
            WorkforceRosterReconciliation.Evaluate(tenantId, roster, jobs, members,
                DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)));
    }

    const int MaximumRows = 500;

    static Result<IReadOnlyList<WorkforceReconciliationObservationView>> Failure(RequestError error) =>
        Result<IReadOnlyList<WorkforceReconciliationObservationView>>.Failure(error);

    static async ValueTask<List<T>?> ReadAllAsync<T>(
        Func<string?, CancellationToken, ValueTask<Page<T>>> read, CancellationToken ct)
    {
        var items = new List<T>();
        string? cursor = null;
        do
        {
            var page = await read(cursor, ct).ConfigureAwait(false);
            items.AddRange(page.Items);
            if (items.Count > MaximumRows)
                return null;
            cursor = page.NextCursor;
        } while (cursor is not null);
        return items;
    }
}
