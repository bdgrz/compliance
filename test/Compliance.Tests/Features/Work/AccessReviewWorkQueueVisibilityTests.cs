using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class AccessReviewWorkQueueVisibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldHideRestrictedCampaignWorkFromActorGivenNoCurrentSystemVisibility(
        bool actorOwnsWork)
    {
        // Arrange
        await using var scenario = await CreateScenarioAsync(allowSystemRead: false);
        var actorUserId = actorOwnsWork ? scenario.Fixture.OwnerUserId :
            scenario.Fixture.LeadUserId;
        var actorMemberId = actorOwnsWork ? scenario.Fixture.OwnerMemberId :
            scenario.Fixture.LeadMemberId;

        // Act
        var result = await ReadQueueAsync(scenario, actorUserId);
        var detail = await scenario.CreateQueue().FindAsync(scenario.Fixture.TenantId,
            scenario.Fixture.ProgramId,
            new OperationsActor(actorUserId, actorMemberId, actorOwnsWork ? "Owner" : "Lead"),
            scenario.Source.Candidate.WorkItemId, CancellationToken.None);
        var assignmentHandler = new AssignWorkItemHandler(
            scenario.Scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(),
            scenario.CreateQueue(), TimeProvider.System);
        var assignment = await assignmentHandler.HandleAsync(new RequestContext<AssignWorkItem>(
            new AssignWorkItem(scenario.Fixture.TenantId, scenario.Fixture.ProgramId,
                scenario.Source.Candidate.WorkItemId, 0, scenario.Fixture.OwnerMemberId),
            ProgramManagementServices.Actor(scenario.Fixture.LeadUserId)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), result.Value.Counts);
        Assert.False(detail.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, detail.Error.Kind);
        Assert.False(assignment.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, assignment.Error.Kind);
    }

    [Fact]
    public async Task ShouldHideRestrictedRemediationFromListSearchCountDetailAndAssignmentGivenNoCurrentSystemVisibility()
    {
        // Arrange
        await using var scenario = await CreateScenarioAsync(allowSystemRead: false,
            workKind: WorkSource.AccessReviewRemediation);
        var actorUserId = scenario.Fixture.LeadUserId;
        var actor = ProgramManagementServices.Actor(actorUserId);
        var list = await new ListWorkHandler(scenario.CreateQueue()).HandleAsync(
            new RequestContext<ListWork>(new ListWork(scenario.Fixture.TenantId,
                scenario.Fixture.ProgramId, "all"), actor), CancellationToken.None);
        var search = await ReadQueueAsync(scenario, actorUserId);
        var detail = await new GetWorkItemHandler(scenario.CreateQueue()).HandleAsync(
            new RequestContext<GetWorkItem>(new GetWorkItem(scenario.Fixture.TenantId,
                scenario.Fixture.ProgramId, scenario.Source.Candidate.WorkItemId), actor),
            CancellationToken.None);
        var assignmentHandler = new AssignWorkItemHandler(
            scenario.Scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(),
            scenario.CreateQueue(), TimeProvider.System);

        // Act
        var assignment = await assignmentHandler.HandleAsync(new RequestContext<AssignWorkItem>(
            new AssignWorkItem(scenario.Fixture.TenantId, scenario.Fixture.ProgramId,
                scenario.Source.Candidate.WorkItemId, 0, scenario.Fixture.OwnerMemberId),
            actor), CancellationToken.None);

        // Assert
        Assert.Equal(WorkSource.AccessReviewRemediation, scenario.Source.Candidate.Kind);
        Assert.True(list.IsSuccess);
        Assert.Empty(list.Value.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), list.Value.Counts);
        Assert.True(search.IsSuccess);
        Assert.Empty(search.Value.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), search.Value.Counts);
        Assert.False(detail.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, detail.Error.Kind);
        Assert.False(assignment.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, assignment.Error.Kind);
    }

    [Fact]
    public async Task ShouldExposeOrphanedCampaignWorkGivenResponsibleReviewerLosesProgramRead()
    {
        // Arrange
        await using var scenario = await CreateScenarioAsync(allowSystemRead: true);
        scenario.Permissions.DeniedProgramReaders.Add(scenario.Fixture.OwnerMemberId);

        // Act
        var result = await ReadQueueAsync(scenario, scenario.Fixture.LeadUserId);

        // Assert
        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.True(item.IsOrphaned);
        Assert.Null(item.AssigneeMemberId);
    }

    [Fact]
    public async Task ShouldRecheckRestrictedVisibilityGivenSystemGrantRevoked()
    {
        // Arrange
        await using var scenario = await CreateScenarioAsync(allowSystemRead: true);
        var grants = scenario.VisibilityGrants;
        var scope = new AccessGrantScope(AccessGrantScopeKind.SystemInstance,
            scenario.SystemInstanceId);
        var actor = scenario.Fixture.LeadUserId;
        var visible = await ReadQueueAsync(scenario, actor);
        Assert.Equal(1, visible.Value.Counts.Total);
        grants.AllowedScopes.Remove(scope);

        // Act
        var hidden = await ReadQueueAsync(scenario, actor);
        var detail = await scenario.CreateQueue().FindAsync(scenario.Fixture.TenantId,
            scenario.Fixture.ProgramId,
            new OperationsActor(actor, scenario.Fixture.LeadMemberId, "Lead"),
            scenario.Source.Candidate.WorkItemId, CancellationToken.None);

        // Assert
        Assert.True(hidden.IsSuccess);
        Assert.Empty(hidden.Value.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), hidden.Value.Counts);
        Assert.False(detail.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, detail.Error.Kind);
    }

    [Fact]
    public async Task ShouldKeepSelfReviewInAssignedQueueGivenExactScopeWaiverIsRequired()
    {
        // Arrange
        await using var scenario = await CreateScenarioAsync(allowSystemRead: true,
            requiresSeparationOfDutiesWaiver: true);
        var actor = new OperationsActor(scenario.Fixture.OwnerUserId,
            scenario.Fixture.OwnerMemberId, "Owner");
        var queue = scenario.CreateQueue();
        var context = new RequestContext<ListWork>(new ListWork(scenario.Fixture.TenantId,
            scenario.Fixture.ProgramId, ListWorkHandler.Mine),
            ProgramManagementServices.Actor(scenario.Fixture.OwnerUserId));

        // Act
        var result = await new ListWorkHandler(queue).HandleAsync(context, CancellationToken.None);
        var snapshot = await queue.ReadAsync(scenario.Fixture.TenantId,
            scenario.Fixture.ProgramId, actor, WorkQueueReader.DefaultHorizonDays,
            CancellationToken.None);
        var eligible = await queue.IsEligibleAsync(scenario.Fixture.TenantId,
            scenario.Source.Candidate, scenario.Fixture.OwnerMemberId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(scenario.Fixture.OwnerMemberId, item.AssigneeMemberId);
        Assert.False(item.IsOrphaned);
        Assert.True(item.RequiresSeparationOfDutiesWaiver);
        Assert.True(snapshot.IsSuccess);
        Assert.False(Assert.Single(snapshot.Value.Entries).ActorEligible);
        Assert.False(eligible);
    }

    static async Task<Result<WorkQueueView>> ReadQueueAsync(QueueScenario scenario,
        Uuid actorUserId) => await new ListWorkHandler(scenario.CreateQueue()).HandleAsync(
            new RequestContext<ListWork>(new ListWork(scenario.Fixture.TenantId,
                scenario.Fixture.ProgramId, "all", Search: "restricted payroll"),
                ProgramManagementServices.Actor(actorUserId)), CancellationToken.None);

    static async Task<QueueScenario> CreateScenarioAsync(bool allowSystemRead,
        bool requiresSeparationOfDutiesWaiver = false,
        string workKind = WorkSource.AccessReviewReview)
    {
        var fixture = await OperationsFixture.CreateAsync();
        var systemInstanceId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var application = new DeclaredApplication(fixture.TenantId, applicationId);
        Assert.True(application.Declare("Payroll", "Run payroll", null,
            fixture.LeadMemberId, "Lead", DateTimeOffset.UtcNow, isRestricted: true).IsSuccess);
        var instance = new DeclaredSystemInstance(fixture.TenantId, systemInstanceId);
        Assert.True(instance.Declare(applicationId, "Production", "aws_account", null, null,
            fixture.LeadMemberId, "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var source = new AccessReviewWorkItems(Candidate(fixture, systemInstanceId,
            requiresSeparationOfDutiesWaiver, workKind));
        var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var aggregateReader = new ScopedAggregateReader(
            services.GetRequiredService<IAggregateReader>(), application, instance);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var consistency = new WorkQueueReadConsistency(events, [source]);
        var permissions = new QueuePermissions();
        var grants = new MutableVisibilityGrants();
        if (allowSystemRead)
            grants.AllowedScopes.Add(new AccessGrantScope(AccessGrantScopeKind.SystemInstance,
                systemInstanceId));
        var visibility = new RestrictedApplicationVisibility(permissions, grants,
            aggregateReader);
        var eligibility = new AccessReviewQueueEligibility(aggregateReader, permissions,
            permissions, visibility);
        WorkQueueReader CreateQueue() => new(aggregateReader,
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [source], accessReviewQueueEligibility: eligibility);
        return new QueueScenario(fixture, scope, CreateQueue, source, grants, permissions,
            systemInstanceId);
    }

    static WorkCandidate Candidate(OperationsFixture fixture, Uuid systemInstanceId,
        bool requiresSeparationOfDutiesWaiver, string kind)
    {
        var campaignId = Uuid.CreateVersion4();
        var itemId = Uuid.CreateVersion4();
        var identity = Uuid.CreateVersion5(campaignId, itemId.ToString());
        var isReview = kind == WorkSource.AccessReviewReview;
        HashSet<Uuid> excluded = [];
        if (isReview && requiresSeparationOfDutiesWaiver)
            excluded.Add(fixture.OwnerMemberId);
        return new WorkCandidate(WorkCandidate.IdFor(identity, kind),
            kind, itemId, null, null, isReview ? "Restricted payroll review" :
                "Restricted payroll remediation",
            isReview ? "Review restricted payroll access." :
                "Remediate restricted payroll access.", fixture.Today.AddDays(7), null,
            isReview ? "record_decision" : "record_remediation_change",
            $"/api/v1/tenants/{fixture.TenantId}/" +
            $"access-review-campaigns/{campaignId}/items/{itemId}/" +
            (isReview ? "decisions" : "remediation-changes"),
            new OperatingHolder(OperatingAuthority.MemberHolder, fixture.OwnerMemberId), null,
            excluded, DateTimeOffset.UtcNow)
        {
            ProgramId = fixture.ProgramId,
            RestrictedSystemInstanceId = systemInstanceId,
            RequiresSeparationOfDutiesWaiver = isReview && requiresSeparationOfDutiesWaiver,
        };
    }

    sealed record QueueScenario(OperationsFixture Fixture, AsyncServiceScope Scope,
        Func<WorkQueueReader> CreateQueue, AccessReviewWorkItems Source,
        MutableVisibilityGrants VisibilityGrants, QueuePermissions Permissions,
        Uuid SystemInstanceId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Scope.DisposeAsync();
    }

    sealed class AccessReviewWorkItems(WorkCandidate candidate)
        : IAccountableWorkItemDirectoryReader
    {
        public WorkCandidate Candidate => candidate;
        public string ProjectorName => "TestAccessReviewCampaignWorkItems";
        public IReadOnlyCollection<string> ProjectedKinds => [candidate.Kind];
        public EventStreamPattern SourcePattern(Uuid tenantId) =>
            EventStreamPattern.ForPattern(tenantId.ToString(), "access-review-campaigns");
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) =>
            ValueTask.FromResult(ProjectionCheckpoint.Start);
        public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
            Uuid programId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<IReadOnlyList<WorkCandidate>>.Success(
                candidate.ProgramId == programId ? [candidate] : []));
    }

    sealed class ScopedAggregateReader(IAggregateReader inner, DeclaredApplication application,
        DeclaredSystemInstance instance) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (aggregate is DeclaredApplication requestedApplication &&
                requestedApplication.Id == application.Id)
                return ValueTask.FromResult((TAggregate)(Aggregate)application);
            if (aggregate is DeclaredSystemInstance requestedInstance &&
                requestedInstance.Id == instance.Id)
                return ValueTask.FromResult((TAggregate)(Aggregate)instance);
            return inner.HydrateAsync(aggregate, ct);
        }
    }

    sealed class QueuePermissions : IAccessGrantPermissionAuthorizer, IPermissionAuthorizer
    {
        public HashSet<Uuid> DeniedProgramReaders { get; } = [];

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(permission == IProgramReadRequest.ReadPermission &&
                                 !DeniedProgramReaders.Contains(memberId));

        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProgramAccessVisibility(true, new HashSet<Uuid>()));

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(false);
    }

    sealed class MutableVisibilityGrants : IAccessGrantScopePermissionAuthorizer
    {
        public HashSet<AccessGrantScope> AllowedScopes { get; } = [];

        public ValueTask<bool> IsAllowedAtAnyScopeAsync(Uuid tenantId, Uuid userId,
            Uuid memberId, IReadOnlyCollection<AccessGrantScope> scopes, string permission,
            CancellationToken ct = default) =>
            ValueTask.FromResult(scopes.Any(AllowedScopes.Contains));

        public ValueTask<bool> IsAllowedAtAnyApplicationInventoryScopeAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(false);
    }
}
