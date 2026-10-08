using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class DirectoryReevaluationDiscovery(IDomainEventReader events, IAggregateReader reader,
    IActualStaffEngagementLocatorReader locators, IRequestBus bus)
{
    public async ValueTask ReconcileStatusAsync(Uuid tenantId, IReactorContext<FirmStaffChangeRecorded> context, CancellationToken ct)
    {
        var sources = await AcceptanceSourcesAsync(tenantId, ct).ConfigureAwait(false);
        var checkpoint = await locators.LoadCheckpointAsync(tenantId, ct).ConfigureAwait(false);
        var coveredPosition = checkpoint.Cursor == EventCursor.Start ? -1 :
            sources.FindIndex(record => record.NextCursor == checkpoint.Cursor);
        var lastAcceptance = sources.FindLastIndex(record => record.Event is ServiceEngagementAcceptanceRecorded);
        if (lastAcceptance > coveredPosition)
            throw Pending("The actual assignment locator has not covered authoritative acceptance sources.");
        var accepted = sources.Where(record => record.Event is ServiceEngagementAcceptanceRecorded).ToArray();
        var ledger = await reader.HydrateAsync(new IndependenceLedger(tenantId), ct).ConfigureAwait(false);
        foreach (var original in accepted)
            if (!ledger.MatchesAcceptanceSource((ServiceEngagementAcceptanceRecorded)original.Event))
                throw new InvalidOperationException("Assignment discovery requires exact retained acceptance source.");
        var expected = accepted.Where(record => ((ServiceEngagementAcceptanceRecorded)record.Event).Acceptance.Assignments
            .Any(staff => staff.StaffMemberId == context.Trigger.Staff.StaffMemberId && staff.UserId == context.Trigger.Staff.UserId &&
                staff.Practice == context.Trigger.Staff.Practice))
            .Select(record => LocatorId((ServiceEngagementAcceptanceRecorded)record.Event, context.Trigger.Staff.StaffMemberId,
                context.Trigger.Staff.UserId)).ToHashSet();
        var seen = new HashSet<Uuid>();
        string? cursor = null;
        do
        {
            var page = await locators.ListForStaffAsync(tenantId, context.Trigger.Staff.StaffMemberId,
                context.Trigger.Staff.UserId, 200, cursor, ct).ConfigureAwait(false);
            foreach (var row in page.Items)
            {
                var source = accepted.SingleOrDefault(record => record.ResourceOffset == row.SourceResourceOffset &&
                    record.Stream.Realm == row.SourceRealm && record.Stream.Area == row.SourceArea && record.Stream.Resource == row.SourceResource);
                if (source?.Event is not ServiceEngagementAcceptanceRecorded acceptance || row.TenantId != tenantId ||
                    row.AcceptanceRequestId != acceptance.RequestId || row.EngagementId != acceptance.Acceptance.EngagementId ||
                    row.AcceptanceSourceSequence != acceptance.ExpectedSequence + 1 || row.AcceptanceRevision != acceptance.Acceptance.Revision ||
                    row.AcceptanceSha256 != IndependenceSourceDigest.Acceptance(acceptance.Acceptance) ||
                    row.SourcePayloadSha256 != IndependenceSourceDigest.AcceptanceEvent(acceptance) ||
                    !acceptance.Acceptance.Assignments.Any(staff => staff.StaffMemberId == row.StaffMemberId &&
                        staff.UserId == row.UserId && staff.Practice == row.Practice && staff.DirectoryStaffRevision == row.DirectoryStaffRevision &&
                        staff.AssignedAt == row.AssignedAt) || row.StaffMemberId != context.Trigger.Staff.StaffMemberId ||
                    row.UserId != context.Trigger.Staff.UserId || row.Practice != context.Trigger.Staff.Practice)
                    throw new InvalidOperationException("An assignment locator must match its exact authoritative acceptance.");
                if (row.LocatorId != LocatorId(acceptance, row.StaffMemberId, row.UserId) || !expected.Contains(row.LocatorId))
                    throw new InvalidOperationException("An assignment locator must preserve its exact immutable source identity.");
                if (!seen.Add(row.LocatorId))
                    throw Pending("Assignment discovery returned a duplicate immutable source row.");
                if (row.DirectoryStaffRevision < context.Trigger.Staff.Revision)
                    await bus.SendReactionAsync(Command(tenantId, context.Source, source, row.StaffMemberId, row.UserId), context, ct)
                        .ConfigureAwait(false);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
        if (!expected.SetEquals(seen))
            throw Pending("Assignment discovery has not returned every authoritative selected staff source.");
        var after = (await AcceptanceSourcesAsync(tenantId, ct).ConfigureAwait(false))
            .Where(record => record.Event is ServiceEngagementAcceptanceRecorded).ToArray();
        if (!accepted.Select(SourceIdentity).SequenceEqual(after.Select(SourceIdentity)))
            throw Pending("Authoritative acceptance sources changed while assignment discovery was paged.");
    }

    public async ValueTask ReconcileAcceptanceAsync(IReactorContext<ServiceEngagementAcceptanceRecorded> context, CancellationToken ct)
    {
        var acceptance = context.Trigger;
        var ledger = await reader.HydrateAsync(new IndependenceLedger(acceptance.TenantId), ct).ConfigureAwait(false);
        if (!ledger.MatchesAcceptanceSource(acceptance))
            throw new InvalidOperationException("Reconciliation requires the exact retained original acceptance.");
        var directory = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        await foreach (var source in events.ReadAsync(directory.Stream, 0, ct).WithCancellation(ct).ConfigureAwait(false))
        {
            if (source.Stream != directory.Stream)
                throw new InvalidOperationException("A directory cause must originate on its authoritative stream.");
            if (source.Event is not FirmStaffChangeRecorded { Operation: "status" } change)
                continue;
            if (!directory.MatchesRetainedStatus(change))
                throw Pending("Directory source history changed while accepted assignments were reconciled.");
            foreach (var staff in acceptance.Acceptance.Assignments.Where(staff =>
                         staff.StaffMemberId == change.Staff.StaffMemberId && staff.UserId == change.Staff.UserId &&
                         staff.Practice == change.Staff.Practice && staff.DirectoryStaffRevision < change.Staff.Revision))
                await bus.SendReactionAsync(Command(acceptance.TenantId, source, context.Source,
                    staff.StaffMemberId, staff.UserId), context, ct).ConfigureAwait(false);
        }
    }

    async ValueTask<List<DomainEventRecord>> AcceptanceSourcesAsync(Uuid tenantId, CancellationToken ct)
    {
        var records = new List<DomainEventRecord>();
        var stream = new IndependenceLedger(tenantId).Stream;
        await foreach (var source in events.ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(), "client-independence"),
                           EventCursor.Start, ct).WithCancellation(ct).ConfigureAwait(false))
        {
            if (source.Stream != stream)
                throw new InvalidOperationException("Accepted source coverage requires its exact owning client ledger.");
            records.Add(source);
        }
        return records;
    }

    static Uuid LocatorId(ServiceEngagementAcceptanceRecorded source, Uuid staffMemberId, Uuid userId) =>
        Uuid.CreateVersion5(source.RequestId,
            $"actual_staff_locator_v1:{source.TenantId}:{source.Acceptance.EngagementId}:{source.Acceptance.Revision}:{staffMemberId}:{userId}");

    static (Uuid EventId, ulong Offset, string Digest) SourceIdentity(DomainEventRecord source) =>
        (source.Event.Metadata.EventId, source.ResourceOffset,
            IndependenceSourceDigest.AcceptanceEvent((ServiceEngagementAcceptanceRecorded)source.Event));

    static RecordDirectoryIndependenceReevaluation Command(Uuid tenantId, DomainEventRecord directory,
        DomainEventRecord acceptance, Uuid staffMemberId, Uuid userId) => new(tenantId,
            directory.ResourceOffset, directory.Event.Metadata.EventId,
            IndependenceSourceDigest.DirectoryEvent((FirmStaffChangeRecorded)directory.Event),
            acceptance.ResourceOffset, acceptance.Event.Metadata.EventId,
            ((ServiceEngagementAcceptanceRecorded)acceptance.Event).RequestId,
            IndependenceSourceDigest.AcceptanceEvent((ServiceEngagementAcceptanceRecorded)acceptance.Event), staffMemberId, userId);

    static ReactionCommandFailedException Pending(string reason) => new(new RequestError(RequestErrorKind.Conflict,
        reason, isTransient: true));
}
