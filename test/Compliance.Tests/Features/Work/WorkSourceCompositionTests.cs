using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.PolicyDistribution;
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
        await using var provider = CreateProvider(fixture);
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

    [Fact]
    public async Task ShouldRemoveManagementWorkFromMineGivenRetainedAttestHistory()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        var permitted = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);
        var blocked = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);
        await using var provider = CreateProvider(fixture);
        RequestScenario Scenario() => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.OwnerUserId));
        CompleteCorrectiveAction Complete(FindingView finding) => new(fixture.TenantId, fixture.ProgramId,
            finding.FindingId, finding.Revision, Assert.Single(finding.CorrectiveActions).ActionId,
            "Removed.", OperationsFixture.FullSupport);
        await Scenario().When(Complete(permitted)).ExpectSuccess();
        await CatchUpAsync(provider, fixture.TenantId);
        var before = await Scenario().When(new ListWork(fixture.TenantId, fixture.ProgramId, "mine"))
            .ExpectSuccess();
        var item = Assert.Single(before.Value.Items);
        Assert.Equal("corrective_action", item.Kind);
        Assert.Equal(1, before.Value.Counts.Total);

        // Act
        await AttestAssignmentHistoryFixture.SeedAsync(provider, fixture.TenantId, fixture.OwnerUserId,
            revoked: true);
        var denied = await Scenario().When(Complete(blocked)).ExpectFailure(RequestErrorKind.Forbidden);
        var after = await Scenario().When(new ListWork(fixture.TenantId, fixture.ProgramId, "mine"))
            .ExpectSuccess();

        // Assert
        Assert.Contains("Attest", denied.Error!.Message, StringComparison.Ordinal);
        Assert.Empty(after.Value.Items);
        Assert.Equal(0, after.Value.Counts.Total);
        await Scenario().When(new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);
    }

    [Fact]
    public async Task ShouldPreserveOversightButDenyManagementAssignmentGivenManagersAttestHistory()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        fixture.Permissions.Managers.Add(fixture.OwnerMemberId);
        await fixture.ProposeAsync(0);
        await using var provider = CreateProvider(fixture);
        await CatchUpAsync(provider, fixture.TenantId);
        RequestScenario Scenario(Uuid user) => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(user));
        var before = await Scenario(fixture.ApproverUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned")).ExpectSuccess();
        var item = Assert.Single(before.Value.Items);
        Assert.Equal("control_operating_plan_approval", item.Kind);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, fixture.TenantId, fixture.ApproverUserId);

        // Act
        var oversight = await Scenario(fixture.ApproverUserId)
            .When(new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId)).ExpectSuccess();
        await Scenario(fixture.ApproverUserId).When(new AssignWorkItem(fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, 0, fixture.OwnerMemberId)).ExpectFailure(RequestErrorKind.Forbidden);
        var assigned = await Scenario(fixture.LeadUserId).When(new AssignWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 0, fixture.OwnerMemberId)).ExpectSuccess();

        // Assert
        Assert.Null(oversight.Value.Item.AssigneeMemberId);
        Assert.Equal(fixture.OwnerMemberId, assigned.Value.Item.AssigneeMemberId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldInvalidateRecordedAssigneeAndAllowReplacementGivenActualAttestHistory(bool revoked)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        fixture.Permissions.Managers.Add(fixture.OwnerMemberId);
        var plan = await fixture.ProposeAsync(0);
        await using var provider = CreateProvider(fixture);
        await CatchUpAsync(provider, fixture.TenantId);
        RequestScenario Scenario(Uuid user) => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(user));
        var before = await Scenario(fixture.ApproverUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned")).ExpectSuccess();
        var item = Assert.Single(before.Value.Items);
        await Scenario(fixture.LeadUserId).When(new AssignWorkItem(fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, 0, fixture.ApproverMemberId)).ExpectSuccess();
        await AttestAssignmentHistoryFixture.SeedAsync(provider, fixture.TenantId, fixture.ApproverUserId,
            revoked: revoked);

        // Act
        await Scenario(fixture.ApproverUserId).When(new ApproveControlOperatingPlan(fixture.TenantId,
            fixture.ProgramId, fixture.ControlId, plan.Revision, plan.PlanVersionId,
            "Independent approval.")).ExpectFailure(RequestErrorKind.Forbidden);
        var mine = await Scenario(fixture.ApproverUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "mine")).ExpectSuccess();
        var orphan = await Scenario(fixture.OwnerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned")).ExpectSuccess();
        await Scenario(fixture.ApproverUserId).When(new DelegateWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 1, fixture.OwnerMemberId, "Replacement needed."))
            .ExpectFailure(RequestErrorKind.Forbidden);
        await Scenario(fixture.ApproverUserId).When(new EscalateWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 1, "Replacement needed."))
            .ExpectFailure(RequestErrorKind.Forbidden);
        await Scenario(fixture.LeadUserId).When(new AssignWorkItem(fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, 1, fixture.ApproverMemberId)).ExpectFailure(RequestErrorKind.Validation);
        await Scenario(fixture.LeadUserId).When(new AssignWorkItem(fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, 1, fixture.OwnerMemberId)).ExpectSuccess();
        await Scenario(fixture.OwnerUserId).When(new ApproveControlOperatingPlan(fixture.TenantId,
            fixture.ProgramId, fixture.ControlId, plan.Revision, plan.PlanVersionId,
            "Independent replacement approval.")).ExpectSuccess();
        await CatchUpAsync(provider, fixture.TenantId);
        var after = await Scenario(fixture.OwnerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "mine")).ExpectSuccess();

        // Assert
        Assert.Empty(mine.Value.Items);
        Assert.Equal(0, mine.Value.Counts.Total);
        Assert.Null(Assert.Single(orphan.Value.Items).AssigneeMemberId);
        Assert.Equal(1, orphan.Value.Counts.Total);
        Assert.DoesNotContain(after.Value.Items, candidate => candidate.WorkItemId == item.WorkItemId);
        Assert.All(after.Value.Items, candidate => Assert.Equal("control_occurrence", candidate.Kind));
    }

    [Theory]
    [InlineData("advisory", false)]
    [InlineData("attest", true)]
    public async Task ShouldPreserveManagementWorkGivenHistoryOutsideTheClientsAttestWall(string practice,
        bool otherTenant)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        var finding = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);
        await using var provider = CreateProvider(fixture);
        await CatchUpAsync(provider, fixture.TenantId);
        await AttestAssignmentHistoryFixture.SeedAsync(provider,
            otherTenant ? Uuid.CreateVersion4() : fixture.TenantId, fixture.OwnerUserId, practice: practice);
        RequestScenario Scenario() => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.OwnerUserId));

        // Act
        var mine = await Scenario().When(new ListWork(fixture.TenantId, fixture.ProgramId, "mine"))
            .ExpectSuccess();
        var completed = await Scenario().When(new CompleteCorrectiveAction(fixture.TenantId,
            fixture.ProgramId, finding.FindingId, finding.Revision,
            Assert.Single(finding.CorrectiveActions).ActionId, "Removed.", OperationsFixture.FullSupport))
            .ExpectSuccess();
        await CatchUpAsync(provider, fixture.TenantId);
        var after = await Scenario().When(new ListWork(fixture.TenantId, fixture.ProgramId, "mine"))
            .ExpectSuccess();

        // Assert
        Assert.Equal(1, mine.Value.Counts.Total);
        Assert.Equal(fixture.OwnerMemberId, Assert.Single(mine.Value.Items).AssigneeMemberId);
        Assert.Equal("completed", Assert.Single(completed.Value.CorrectiveActions).Status);
        Assert.Empty(after.Value.Items);
    }

    [Fact]
    public async Task ShouldPreserveTeamOversightButDenyClaimGivenActualAttestHistory()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        fixture.Permissions.Managers.Add(fixture.OwnerMemberId);
        await fixture.AddToTeamAsync(fixture.OwnerUserId);
        await fixture.AddToTeamAsync(fixture.OutsiderUserId);
        await fixture.PlanAsync(new OperatingHolder(OperatingAuthority.TeamHolder, fixture.TeamId));
        await using var provider = CreateProvider(fixture);
        await CatchUpAsync(provider, fixture.TenantId);
        RequestScenario Scenario(Uuid user) => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(user));
        var before = await Scenario(fixture.OwnerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "team")).ExpectSuccess();
        var item = before.Value.Items[0];
        Assert.Null(item.AssigneeMemberId);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, fixture.TenantId, fixture.OwnerUserId);

        // Act
        var team = await Scenario(fixture.OwnerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "team")).ExpectSuccess();
        var all = await Scenario(fixture.OwnerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "all")).ExpectSuccess();
        var unassigned = await Scenario(fixture.OwnerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned")).ExpectSuccess();
        await Scenario(fixture.OwnerUserId).When(new ClaimWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 0)).ExpectFailure(RequestErrorKind.Forbidden);
        await Scenario(fixture.OwnerUserId).When(new AssignWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 0, fixture.Member(fixture.OutsiderUserId)))
            .ExpectFailure(RequestErrorKind.Forbidden);
        var claimed = await Scenario(fixture.OutsiderUserId).When(new ClaimWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 0)).ExpectSuccess();

        // Assert
        Assert.Equal(before.Value.Counts, team.Value.Counts);
        Assert.Equal(before.Value.Items, team.Value.Items);
        Assert.Equal(team.Value.Counts, all.Value.Counts);
        Assert.Empty(unassigned.Value.Items);
        Assert.Equal(0, unassigned.Value.Counts.Total);
        Assert.Equal(fixture.Member(fixture.OutsiderUserId), claimed.Value.Item.AssigneeMemberId);
    }

    [Fact]
    public async Task ShouldFailClosedGivenRetainedMemberUserDisagreesWithCanonicalMemberIdentity()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        var finding = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);
        await using var provider = CreateProvider(fixture);
        await CatchUpAsync(provider, fixture.TenantId);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, fixture.TenantId, fixture.OwnerUserId);
        {
            var registrations = new List<DomainEvent>();
            await foreach (var record in provider.GetRequiredService<IDomainEventReader>().ReadAsync(
                               EventStreamPattern.ForPattern(fixture.TenantId.ToString(), "rbac-members",
                                   fixture.OwnerMemberId.ToString()), ProjectionCheckpoint.Start.Cursor, CancellationToken.None))
                registrations.Add(record.Event);
            Assert.IsType<MemberRegistered>(Assert.Single(registrations));
            // Deliberately malformed retained provenance: public registration cannot produce this identity.
            DomainEvent malformed = new MemberRegistered(fixture.TenantId, fixture.OwnerMemberId,
                fixture.OutsiderUserId, "client_personnel", Uuid.CreateVersion4());
            malformed.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), fixture.OwnerMemberId,
                2, DateTimeOffset.UtcNow));
            await provider.GetRequiredService<IEventStore>().AppendAsync(new EventStreamAddress(
                fixture.TenantId.ToString(), "rbac-members", fixture.OwnerMemberId.ToString()),
                1, [malformed]);
        }
        RequestScenario Scenario() => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.OwnerUserId));

        // Act
        await Scenario().When(new CompleteCorrectiveAction(fixture.TenantId, fixture.ProgramId,
            finding.FindingId, finding.Revision, Assert.Single(finding.CorrectiveActions).ActionId,
            "Removed.", OperationsFixture.FullSupport)).ExpectFailure(RequestErrorKind.Forbidden);
        var mine = await Scenario().When(new ListWork(fixture.TenantId, fixture.ProgramId, "mine"))
            .ExpectSuccess();

        // Assert
        Assert.Empty(mine.Value.Items);
        Assert.Equal(0, mine.Value.Counts.Total);
    }

    [Fact]
    public async Task ShouldCoverEveryProductionKindGivenSourceManagementRequestMapping()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        await using var provider = CreateProvider(fixture);
        await using var scope = provider.CreateAsyncScope();
        var kinds = scope.ServiceProvider.GetServices<IAccountableWorkItemDirectoryReader>()
            .SelectMany(reader => reader.ProjectedKinds).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        // Act
        var mapped = WorkSourceManagement.SourceRequests.Keys.Order(StringComparer.Ordinal);

        // Assert
        Assert.Equal(kinds, mapped);
        Assert.All(WorkSourceManagement.SourceRequests.Values.SelectMany(types => types),
            type => Assert.True(typeof(IRequestBase).IsAssignableFrom(type)));
        Assert.Equal(typeof(AcknowledgePolicy), Assert.Single(
            WorkSourceManagement.SourceRequests[PolicyCampaignWork.Acknowledgement]));
        Assert.False(typeof(IClientManagementMutationRequest).IsAssignableFrom(typeof(AcknowledgePolicy)));
    }

    static ServiceProvider CreateProvider(OperationsFixture fixture)
    {
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
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
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
