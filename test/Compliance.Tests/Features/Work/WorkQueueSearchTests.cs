using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkQueueSearchTests
{
    [Fact]
    public async Task ShouldReconcileSearchCountsGivenMixedSourceWork()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(-7));
        var evidence = await fixture.AsAsync(fixture.LeadUserId, new OpenEvidenceRequest(
            fixture.TenantId, fixture.ProgramId, "Quarterly access evidence", "Signed export.",
            fixture.OwnerMemberId, fixture.Today.AddDays(4)));

        // Act
        var queue = await fixture.AsAsync(fixture.OwnerUserId, new ListWork(fixture.TenantId,
            fixture.ProgramId, Search: "evidence_request"));

        // Assert
        Assert.Equal(new WorkCountsView(1, 0, 0, 0), queue.Counts);
        var item = Assert.Single(queue.Items);
        Assert.Equal(evidence.EvidenceRequestId, item.SourceId);
        Assert.Equal("fulfil", item.NextAction);
        Assert.EndsWith($"/evidence-requests/{evidence.EvidenceRequestId}/fulfilments",
            item.ActionPath, StringComparison.Ordinal);
    }
    [Theory]
    [InlineData("QUARTERLY", "evidence_request")]
    [InlineData(" signed EXPORT. ", "evidence_request")]
    [InlineData(" CORRECTIVE_ACTION ", "corrective_action")]
    [InlineData("finding", "corrective_action")]
    public async Task ShouldSearchSummaryReasonAndKindGivenCaseAndSurroundingWhitespace(
        string search, string kind)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);
        await fixture.AsAsync(fixture.LeadUserId, new OpenEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, "Quarterly access evidence", "Signed export.",
            fixture.OwnerMemberId, fixture.Today.AddDays(4)));

        // Act
        var queue = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, Search: search));

        // Assert
        Assert.Equal(kind, Assert.Single(queue.Items).Kind);
        Assert.Equal(1, queue.Counts.Total);
        Assert.Equal(kind == "corrective_action" ? 1 : 0, queue.Counts.DueToday);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ShouldPreserveExistingCountsGivenBlankSearch(string? search)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(-7), fixture.Today);

        // Act
        var queue = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, Search: search));

        // Assert
        Assert.Equal(new WorkCountsView(2, 1, 1, 1), queue.Counts);
        Assert.Equal(2, queue.Items.Count);
    }

    [Theory]
    [InlineData("mine", 1)]
    [InlineData("all", 1)]
    [InlineData("team", 0)]
    [InlineData("unassigned", 0)]
    [InlineData("escalated", 1)]
    public async Task ShouldPreserveScopedCountsGivenSearchAcrossQueueScopes(string scope, int count)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(-7));
        await fixture.AsAsync(fixture.LeadUserId, new OpenEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, "Future evidence", "No corrective action match.",
            fixture.OwnerMemberId, fixture.Today.AddDays(4)));

        // Act
        var queue = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, scope, Search: "corrective_action"));

        // Assert
        Assert.Equal(scope, queue.Scope);
        Assert.Equal(new WorkCountsView(count, count, 0, count), queue.Counts);
        Assert.Equal(count, queue.Items.Count);
    }

    [Fact]
    public async Task ShouldReconcileSearchedSourceActionAndRemoveItemGivenEvidenceCancellation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(-7));
        var evidence = await fixture.AsAsync(fixture.LeadUserId, new OpenEvidenceRequest(
            fixture.TenantId, fixture.ProgramId, "Quarterly access evidence", "Signed export.",
            fixture.OwnerMemberId, fixture.Today));
        var before = await fixture.AsAsync(fixture.OwnerUserId, new ListWork(fixture.TenantId,
            fixture.ProgramId, Search: "evidence_request"));
        var item = Assert.Single(before.Items);
        var detail = await fixture.AsAsync(fixture.OwnerUserId,
            new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId));

        // Act
        await fixture.AsAsync(fixture.LeadUserId, new CancelEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, evidence.EvidenceRequestId, evidence.Revision, "No longer required."));
        var after = await fixture.AsAsync(fixture.OwnerUserId, new ListWork(fixture.TenantId,
            fixture.ProgramId, Search: "evidence_request"));
        var remaining = await fixture.AsAsync(fixture.OwnerUserId, new ListWork(fixture.TenantId,
            fixture.ProgramId, Search: "corrective_action"));

        // Assert
        Assert.Equal(item, detail.Item);
        Assert.Equal(new WorkCountsView(1, 0, 1, 0), before.Counts);
        Assert.Empty(after.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), after.Counts);
        Assert.Equal(new WorkCountsView(1, 1, 0, 1), remaining.Counts);
    }

    [Theory]
    [InlineData("Quarterly access evidence")]
    [InlineData("Signed export.")]
    [InlineData("evidence_request")]
    public async Task ShouldNotDiscoverOtherActorsWorkGivenMatchingSearch(string search)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.AsAsync(fixture.LeadUserId, new OpenEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, "Quarterly access evidence", "Signed export.",
            fixture.OwnerMemberId, fixture.Today));

        // Act
        var outsider = await fixture.AsAsync(fixture.OutsiderUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "all", Search: search));
        var foreignTenant = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(Uuid.CreateVersion4(), fixture.ProgramId, "all", Search: search));

        // Assert
        Assert.Empty(outsider.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), outsider.Counts);
        Assert.Empty(foreignTenant.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), foreignTenant.Counts);
    }

    [Fact]
    public async Task ShouldRejectOversizedSearchGivenMoreThanTwoHundredCharacters()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var search = new string('x', 201);

        // Act
        var result = await fixture.Scenario(fixture.OwnerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, Search: search))
            .ExpectFailure(RequestErrorKind.Validation);

        // Assert
        Assert.Contains("200", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReturnEmptyCountsGivenSupportedSearchWithNoVisibleMatch()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);

        // Act
        var queue = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, Search: new string('x', 200)));

        // Assert
        Assert.Empty(queue.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), queue.Counts);
    }
    [Theory]
    [InlineData("", 3, 1, 1, 1)]
    [InlineData("corrective_action", 1, 1, 0, 1)]
    [InlineData("evidence_request", 1, 0, 1, 0)]
    [InlineData("finding_closure_review", 1, 0, 0, 0)]
    public async Task ShouldReconcileProjectedSourceUnionGivenSearchAndCaughtUpCheckpoints(
        string search, int total, int overdue, int dueToday, int escalated)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(-7));
        var remediated = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today);
        await fixture.AsAsync(fixture.OwnerUserId, new CompleteCorrectiveAction(fixture.TenantId,
            fixture.ProgramId, remediated.FindingId, remediated.Revision,
            Assert.Single(remediated.CorrectiveActions).ActionId, "Removed.", OperationsFixture.FullSupport));
        await fixture.AsAsync(fixture.LeadUserId, new OpenEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, "Review export", "Capture proof.", fixture.OwnerMemberId, fixture.Today));
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var client = new InMemoryKvClient();
        var evidence = new FitzEvidenceWorkItemDirectory(client);
        var corrective = new FitzCorrectiveActionWorkItemDirectory(client);
        var closure = new FitzFindingClosureWorkItemDirectory(client);
        await CatchUpAsync(events, fixture.TenantId, evidence, evidence, evidence.ApplyAsync);
        await CatchUpAsync(events, fixture.TenantId, corrective, corrective, corrective.ApplyAsync);
        await CatchUpAsync(events, fixture.TenantId, closure, closure, closure.ApplyAsync);
        IAccountableWorkItemDirectoryReader[] directories = [evidence, corrective, closure];
        var consistency = new WorkQueueReadConsistency(events, directories);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: directories);
        var handler = new ListWorkHandler(queue);
        var context = new RequestContext<ListWork>(new ListWork(fixture.TenantId,
                fixture.ProgramId, "all", Search: search),
            ProgramManagementServices.Actor(fixture.ApproverUserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new WorkCountsView(total, overdue, dueToday, escalated), result.Value.Counts);
        Assert.Equal(total, result.Value.Items.Count);
        Assert.Equal(total, result.Value.Items.Select(static item => item.WorkItemId).Distinct().Count());
        if (search == "finding_closure_review")
            foreach (var ineligibleUser in new[] { fixture.LeadUserId, fixture.OwnerUserId,
                         fixture.OutsiderUserId })
            {
                var hidden = await handler.HandleAsync(new RequestContext<ListWork>(
                    context.Request, ProgramManagementServices.Actor(ineligibleUser)), CancellationToken.None);
                Assert.True(hidden.IsSuccess);
                Assert.Empty(hidden.Value.Items);
                Assert.Equal(new WorkCountsView(0, 0, 0, 0), hidden.Value.Counts);
            }
        foreach (var item in result.Value.Items)
        {
            Assert.True(string.IsNullOrEmpty(search) || item.Kind == search);
            Assert.StartsWith($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/",
                item.ActionPath, StringComparison.Ordinal);
            Assert.Equal(item, (await queue.FindAsync(fixture.TenantId, fixture.ProgramId,
                new OperationsActor(fixture.ApproverUserId, fixture.ApproverMemberId, "Approver"),
                item.WorkItemId, CancellationToken.None)).Value.Entry.Item);
        }
    }

    [Fact]
    public async Task ShouldRejectLaggingSourceGivenSearchWithNoMatchingItems()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.AsAsync(fixture.LeadUserId, new OpenEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, "Review export", "Capture proof.", fixture.OwnerMemberId, fixture.Today));
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var evidence = new FitzEvidenceWorkItemDirectory(new InMemoryKvClient());
        var consistency = new WorkQueueReadConsistency(events, [evidence]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [evidence]);
        var context = new RequestContext<ListWork>(new ListWork(fixture.TenantId,
                fixture.ProgramId, "all", Search: "no such visible work"),
            ProgramManagementServices.Actor(fixture.ApproverUserId));

        // Act
        var result = await new ListWorkHandler(queue).HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.True(result.Error.IsTransient);
    }

    static async Task CatchUpAsync(IDomainEventReader events, Uuid tenantId,
        IAccountableWorkItemDirectoryReader directory, IProjectionStore store,
        Func<DomainEvent, CancellationToken, ValueTask> apply)
    {
        var pattern = directory.SourcePattern(tenantId);
        var checkpoint = await directory.LoadCheckpointAsync(tenantId);
        await using var batch = await store.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(directory.ProjectorName, pattern), checkpoint));
        var cursor = checkpoint.Cursor;
        await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
        {
            await apply(record.Event, CancellationToken.None);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }
}
