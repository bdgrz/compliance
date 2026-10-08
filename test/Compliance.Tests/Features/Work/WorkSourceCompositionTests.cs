using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkSourceCompositionTests
{
    [Fact]
    public async Task ShouldReconcileCountsDetailsAndSourceActionGivenAllProductionReaders()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today.AddDays(-7));
        var remediated = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);
        var verified = await fixture.AsAsync(fixture.OwnerUserId, new CompleteCorrectiveAction(fixture.TenantId,
            fixture.ProgramId, remediated.FindingId, remediated.Revision,
            Assert.Single(remediated.CorrectiveActions).ActionId, "Removed.", OperationsFixture.FullSupport));
        var evidenceRequest = await fixture.AsAsync(fixture.LeadUserId, new OpenEvidenceRequest(
            fixture.TenantId, fixture.ProgramId, "Quarterly evidence", "Reviewed export", fixture.OwnerMemberId,
            fixture.Today));
        var events = fixture.Provider.GetRequiredService<IEventStore>();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance"
        }).Build();
        _ = services.AddCompliance(configuration, developerAuthentication: true);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton(events);
        services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(
            new PermissionBackedAccessGrantPermissionAuthorizer(fixture.Permissions));
        services.AddSingleton<IProgramResourceScopeResolver, TestProgramResourceScopeResolver>();
        services.AddSingleton<ITenantActivity, ActiveTenant>();
        services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var actor = ProgramManagementServices.Actor(fixture.ApproverUserId);
        RequestScenario Scenario() => RequestScenario.For(provider).GivenActor(actor);
        var list = new ListWork(fixture.TenantId, fixture.ProgramId, "all");

        // Act
        var lag = await Scenario().When(list with { Search = "no visible match" }).ExpectFailure(RequestErrorKind.Conflict);
        await CatchUpAsync(provider, fixture.TenantId);
        var before = await Scenario().When(list).ExpectSuccess();
        foreach (var item in before.Value.Items)
        {
            var detail = await Scenario().When(new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId))
                .ExpectSuccess();
            Assert.Equal(item, detail.Value.Item);
            var searched = await Scenario().When(list with { Search = item.Kind }).ExpectSuccess();
            Assert.Equal(item, Assert.Single(searched.Value.Items));
            Assert.Equal(1, searched.Value.Counts.Total);
        }
        var outsider = await RequestScenario.For(provider).GivenActor(ProgramManagementServices.Actor(fixture.OutsiderUserId))
            .When(list with { Search = "Quarterly evidence" }).ExpectSuccess();
        Assert.Empty(outsider.Value.Items);
        Assert.Equal(0, outsider.Value.Counts.Total);
        var evidenceItem = Assert.Single(before.Value.Items, item => item.Kind == "evidence_request");
        await RequestScenario.For(provider).GivenActor(ProgramManagementServices.Actor(fixture.OutsiderUserId))
            .When(new GetWorkItem(fixture.TenantId, fixture.ProgramId, evidenceItem.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);
        await Scenario().When(new CancelEvidenceRequest(fixture.TenantId, fixture.ProgramId,
            evidenceRequest.EvidenceRequestId, evidenceRequest.Revision, "No longer required")).ExpectSuccess();
        var changed = await Scenario().When(list).ExpectFailure(RequestErrorKind.Conflict);
        await CatchUpAsync(provider, fixture.TenantId);
        var after = await Scenario().When(list).ExpectSuccess();

        var closureItem = Assert.Single(after.Value.Items, item => item.Kind == "finding_closure_review");
        await Scenario().When(new AssignWorkItem(fixture.TenantId, fixture.ProgramId, closureItem.WorkItemId,
            0, fixture.ApproverMemberId)).ExpectSuccess();
        await Scenario().When(new CloseFinding(fixture.TenantId, fixture.ProgramId, verified.FindingId,
            verified.Revision, "Independently verified correction", OperationsFixture.FullSupport,
            "Closure accepted")).ExpectSuccess();
        await Scenario().When(list).ExpectFailure(RequestErrorKind.Conflict);
        await CatchUpAsync(provider, fixture.TenantId);
        var final = await Scenario().When(list).ExpectSuccess();
        await Scenario().When(new GetWorkItem(fixture.TenantId, fixture.ProgramId, closureItem.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.True(lag.Error!.IsTransient);
        Assert.True(changed.Error!.IsTransient);
        Assert.Equal(new WorkCountsView(3, 1, 1, 1), before.Value.Counts);
        Assert.Equal(3, before.Value.Items.Select(item => item.WorkItemId).Distinct().Count());
        Assert.Equal(new WorkCountsView(2, 1, 0, 1), after.Value.Counts);
        Assert.DoesNotContain(after.Value.Items, item => item.SourceId == evidenceRequest.EvidenceRequestId);
        Assert.Equal(new WorkCountsView(1, 1, 0, 1), final.Value.Counts);
        Assert.Equal("corrective_action", Assert.Single(final.Value.Items).Kind);
        Assert.Contains(after.Value.Items, item => item.Kind == "corrective_action" && item.NextAction == "complete");
        Assert.Contains(after.Value.Items, item => item.Kind == "finding_closure_review" && item.NextAction == "close");
    }

    static async Task CatchUpAsync(IServiceProvider provider, Uuid tenantId)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var events = services.GetRequiredService<IDomainEventReader>();
        foreach (var reader in services.GetServices<IAccountableWorkItemDirectoryReader>())
        {
            var store = reader as FitzKvProjectionStore ?? services.GetRequiredService<FitzRiskEvaluationDirectory>();
            var method = store.GetType().GetMethod("ApplyAsync", [typeof(DomainEvent), typeof(CancellationToken)]);
            Assert.NotNull(method);
            await ApplyAsync(reader, store, method.CreateDelegate<Func<DomainEvent, CancellationToken, ValueTask>>(store));
        }

        async Task ApplyAsync(IAccountableWorkItemDirectoryReader reader, FitzKvProjectionStore store,
            Func<DomainEvent, CancellationToken, ValueTask> apply)
        {
            var pattern = reader.SourcePattern(tenantId);
            var checkpoint = await reader.LoadCheckpointAsync(tenantId);
            await using var batch = await store.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity(reader.ProjectorName, pattern), checkpoint));
            var cursor = checkpoint.Cursor;
            await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
            {
                await apply(record.Event, CancellationToken.None);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }
    }
}
