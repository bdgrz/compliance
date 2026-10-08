using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution;

public sealed class PolicyAcknowledgementIndependenceTests
{
    [Theory]
    [InlineData(false, "client_personnel")]
    [InlineData(true, "client_personnel")]
    [InlineData(true, "guest")]
    public async Task ShouldDenyManagementProxyGivenCanonicalActualAttestHistory(bool closed, string affiliation)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(affiliation: affiliation);
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Source.TenantId,
            fixture.Source.ManagerUserId, closed);
        var before = await fixture.CampaignAsync();

        // Act
        var result = await fixture.AcknowledgeAsync(fixture.Source.ManagerUserId, fixture.Source.NonMemberPersonId);
        var after = await fixture.CampaignAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("Attest", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Null(after.FindAcknowledgement(fixture.Source.NonMemberPersonId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetainPersonalAcknowledgementGivenActualAttestHistoryAndOnlyReadGrant(bool closed)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Source.TenantId,
            fixture.Source.MemberUserId, closed);

        // Act
        var result = await fixture.AcknowledgeAsync(fixture.Source.MemberUserId, fixture.Source.MemberPersonId);
        var retained = await fixture.CampaignAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(result.Value.RecordedOnBehalf);
        Assert.Equal(RbacIds.Member(fixture.Source.TenantId, fixture.Source.MemberUserId).ToString(), result.Value.Recorder.Id);
        Assert.NotNull(retained.FindAcknowledgement(fixture.Source.MemberPersonId));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("advisory")]
    [InlineData("other_client")]
    public async Task ShouldRetainAttributedProxyGivenEligibleCurrentManager(string history)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        if (history != "none")
            await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider,
                history == "other_client" ? Uuid.CreateVersion4() : fixture.Source.TenantId,
                fixture.Source.ManagerUserId, true, history == "advisory" ? "advisory" : "attest");

        // Act
        var result = await fixture.AcknowledgeAsync(fixture.Source.ManagerUserId, fixture.Source.NonMemberPersonId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(result.Value.RecordedOnBehalf);
        Assert.Equal(fixture.Source.NonMemberPersonId.ToString(), result.Value.Performer.Id);
        Assert.Equal(RbacIds.Member(fixture.Source.TenantId, fixture.Source.ManagerUserId).ToString(), result.Value.Recorder.Id);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("audience")]
    [InlineData("personal")]
    public async Task ShouldPreserveExistingDisclosureBoundaryGivenHistoricalAttestActor(string boundary)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var actor = boundary == "permission" ? fixture.Source.MemberUserId : fixture.Source.ManagerUserId;
        var target = boundary == "personal" ? fixture.Source.MemberPersonId
            : boundary == "audience" ? Uuid.CreateVersion4() : fixture.Source.NonMemberPersonId;
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Source.TenantId, actor);

        // Act
        var result = await fixture.AcknowledgeAsync(actor, target);

        // Assert
        Assert.Equal(boundary == "personal" ? RequestErrorKind.Forbidden : RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.DoesNotContain("Attest", result.Error!.Message, StringComparison.Ordinal);
        if (boundary == "personal")
            Assert.Contains("personally", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("suspended")]
    [InlineData("firm_staff")]
    public async Task ShouldPreserveOrdinaryMembershipDenialGivenProxyWrite(string membership)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(membership: membership);

        // Act
        var result = await fixture.AcknowledgeAsync(fixture.Source.ManagerUserId, fixture.Source.NonMemberPersonId);

        // Assert
        Assert.Equal(membership == "firm_staff" ? RequestErrorKind.Forbidden : RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldPreserveCapturedCorrelationGivenLaterDurableRelinking()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Source.TenantId, fixture.Source.MemberUserId);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var guard = scope.ServiceProvider.GetRequiredService<PolicyAcknowledgementRecorderGuard>();
        var captured = await guard.CaptureAsync(fixture.Source.TenantId, fixture.Source.MemberPersonId);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new Person(fixture.Source.TenantId, fixture.Source.MemberPersonId), person =>
            {
                Assert.Null(person.CorrelateMembership(captured.Revision, null,
                    ActorReference.ForSystemProcess("synthetic-person-source", "Synthetic source"), DateTimeOffset.UtcNow));
                return Result.Success;
            });

        // Act
        var original = await guard.EvaluateCapturedAsync(fixture.Source.TenantId, fixture.Source.MemberUserId, captured);
        var current = await guard.EvaluateAsync(fixture.Source.TenantId, fixture.Source.MemberUserId, fixture.Source.MemberPersonId);

        // Assert
        Assert.True(original.IsSelf);
        Assert.Equal(fixture.Source.MemberUserId, original.Person.CorrelatedUserId);
        Assert.False(current.IsSelf);
        Assert.False(current.CanRecordProxy);
        Assert.Null(current.Person.CorrelatedUserId);
        Assert.True(current.Person.CommittedStreamPosition > original.Person.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRefuseCapturedEligibilityGivenAnotherTenantsSnapshot()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var guard = scope.ServiceProvider.GetRequiredService<PolicyAcknowledgementRecorderGuard>();
        var snapshot = await guard.CaptureAsync(fixture.Source.TenantId, fixture.Source.MemberPersonId);

        // Act
        var result = await guard.EvaluateCapturedAsync(Uuid.CreateVersion4(), fixture.Source.MemberUserId, snapshot);

        // Assert
        Assert.False(result.IsSelf);
        Assert.False(result.CanRecordProxy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReconcileProxyQueueGivenActualSourceDenialAfterAttestHistory(bool revoked)
    {
        // Arrange
        await using (var ordinary = await Fixture.CreateAsync())
        {
            var accepted = await ordinary.AcknowledgeAsync(ordinary.Source.ManagerUserId,
                ordinary.Source.NonMemberPersonId);
            Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        }
        await using var fixture = await Fixture.CreateAsync();
        foreach (var user in new[] { fixture.Source.ManagerUserId, fixture.Source.MemberUserId })
            await ProgramManagementServices.SeedAsync(fixture.Provider, new Member(fixture.Source.TenantId, user),
                member => member.Register());
        await CatchUpWorkAsync(fixture.Provider, fixture.Source.TenantId);
        RequestScenario Scenario() => RequestScenario.For(fixture.Provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.Source.ManagerUserId));
        var before = await Scenario().When(new ListWork(fixture.Source.TenantId, fixture.Source.ProgramId, "mine"))
            .ExpectSuccess();
        var item = Assert.Single(before.Value.Items);
        Assert.Equal("record_acknowledgement", item.NextAction);
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Source.TenantId,
            fixture.Source.ManagerUserId, revoked);

        // Act
        var denied = await fixture.AcknowledgeAsync(fixture.Source.ManagerUserId, fixture.Source.NonMemberPersonId);
        var after = await Scenario().When(new ListWork(fixture.Source.TenantId, fixture.Source.ProgramId, "mine"))
            .ExpectSuccess();
        var oversight = await Scenario().When(new GetWorkItem(fixture.Source.TenantId,
            fixture.Source.ProgramId, item.WorkItemId)).ExpectSuccess();
        await Scenario().When(new AssignWorkItem(fixture.Source.TenantId, fixture.Source.ProgramId,
            item.WorkItemId, 0, RbacIds.Member(fixture.Source.TenantId, fixture.Source.ManagerUserId)))
            .ExpectFailure(RequestErrorKind.Forbidden);
        await Scenario().When(new DelegateWorkItem(fixture.Source.TenantId, fixture.Source.ProgramId,
            item.WorkItemId, 0, RbacIds.Member(fixture.Source.TenantId, fixture.Source.MemberUserId), "Reconcile source eligibility."))
            .ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        Assert.Null(oversight.Value.Item.AssigneeMemberId);
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        Assert.Contains("Attest", denied.Error!.Message, StringComparison.Ordinal);
        Assert.Empty(after.Value.Items);
        Assert.Equal(0, after.Value.Counts.Total);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("advisory")]
    [InlineData("other_client")]
    public async Task ShouldCompleteGenuineProxyWorkGivenSourceEligibleManager(string history)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new Member(fixture.Source.TenantId, fixture.Source.ManagerUserId), member => member.Register());
        if (history != "none")
            await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider,
                history == "other_client" ? Uuid.CreateVersion4() : fixture.Source.TenantId,
                fixture.Source.ManagerUserId, true, history == "advisory" ? "advisory" : "attest");
        await CatchUpWorkAsync(fixture.Provider, fixture.Source.TenantId);
        RequestScenario Scenario() => RequestScenario.For(fixture.Provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.Source.ManagerUserId));
        var before = await Scenario().When(new ListWork(fixture.Source.TenantId,
            fixture.Source.ProgramId, "mine")).ExpectSuccess();
        var item = Assert.Single(before.Value.Items);

        // Act
        var assignment = await Scenario().When(new AssignWorkItem(fixture.Source.TenantId,
            fixture.Source.ProgramId, item.WorkItemId, 0,
            RbacIds.Member(fixture.Source.TenantId, fixture.Source.ManagerUserId))).ExpectFailure(RequestErrorKind.Conflict);
        var recorded = await fixture.AcknowledgeAsync(fixture.Source.ManagerUserId, fixture.Source.NonMemberPersonId);
        await CatchUpWorkAsync(fixture.Provider, fixture.Source.TenantId);
        var after = await Scenario().When(new ListWork(fixture.Source.TenantId,
            fixture.Source.ProgramId, "mine")).ExpectSuccess();

        // Assert
        Assert.Contains("already assigned", assignment.Error!.Message, StringComparison.Ordinal);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        Assert.True(recorded.Value.RecordedOnBehalf);
        Assert.Empty(after.Value.Items);
        Assert.Equal(0, after.Value.Counts.Total);
    }

    [Fact]
    public async Task ShouldPreserveGenuinePersonalQueueGivenAttestHistoryAndReadOnlyAuthority()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new Member(fixture.Source.TenantId, fixture.Source.MemberUserId), member => member.Register());
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Source.TenantId,
            fixture.Source.MemberUserId, revoked: true);
        await CatchUpWorkAsync(fixture.Provider, fixture.Source.TenantId);
        RequestScenario Scenario() => RequestScenario.For(fixture.Provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.Source.MemberUserId));
        var before = await Scenario().When(new ListWork(fixture.Source.TenantId,
            fixture.Source.ProgramId, "mine")).ExpectSuccess();
        var item = Assert.Single(before.Value.Items);

        // Act
        await Scenario().When(new GetWorkItem(fixture.Source.TenantId,
            fixture.Source.ProgramId, item.WorkItemId)).ExpectSuccess();
        var claim = await Scenario().When(new ClaimWorkItem(fixture.Source.TenantId,
            fixture.Source.ProgramId, item.WorkItemId, 0)).ExpectFailure(RequestErrorKind.Validation);
        var recorded = await fixture.AcknowledgeAsync(fixture.Source.MemberUserId, fixture.Source.MemberPersonId);
        await CatchUpWorkAsync(fixture.Provider, fixture.Source.TenantId);
        var after = await Scenario().When(new ListWork(fixture.Source.TenantId,
            fixture.Source.ProgramId, "mine")).ExpectSuccess();

        // Assert
        Assert.Equal("acknowledge", item.NextAction);
        Assert.Equal(1, before.Value.Counts.Mine);
        Assert.Contains("Only team work", claim.Error!.Message, StringComparison.Ordinal);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        Assert.False(recorded.Value.RecordedOnBehalf);
        Assert.Empty(after.Value.Items);
    }

    static async Task CatchUpWorkAsync(IServiceProvider provider, Uuid tenantId)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var events = services.GetRequiredService<IDomainEventReader>();
        foreach (var directory in services.GetServices<IAccountableWorkItemDirectoryReader>())
        {
            var store = directory as FitzKvProjectionStore ?? services.GetRequiredService<FitzRiskEvaluationDirectory>();
            var apply = store.GetType().GetMethod("ApplyAsync", [typeof(DomainEvent), typeof(CancellationToken)])!
                .CreateDelegate<Func<DomainEvent, CancellationToken, ValueTask>>(store);
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

    sealed class Fixture : IAsyncDisposable
    {
        public required PolicyCampaignHandlerTests.Fixture Source { get; init; }
        public required ServiceProvider Provider { get; init; }
        public required PolicyVersionView Version { get; init; }
        public required Uuid CampaignId { get; init; }

        public static async Task<Fixture> CreateAsync(string affiliation = "client_personnel", string membership = "active")
        {
            // Retain real reviewed/approved policy, frozen roster and launched campaign before composing production dispatch.
            var source = await PolicyCampaignHandlerTests.Fixture.CreateAsync();
            var version = await source.ApprovePolicyAsync();
            var campaign = await source.LaunchAsync(version);
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build(), developerAuthentication: true);
            var store = source.Provider.GetRequiredService<IEventStore>();
            services.AddSingleton(store);
            services.AddSingleton<IDomainEventReader>((IDomainEventReader)store);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
                new Permissions(source.ManagerUserId)));
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships(source.TenantId,
                source.ManagerUserId, source.MemberUserId, affiliation, membership));
            var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            await ProgramManagementServices.SeedAsync(provider, new ComplianceProgram(source.TenantId, source.ProgramId), program =>
            {
                Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                    RbacIds.Member(source.TenantId, source.ManagerUserId), "Client manager", DateTimeOffset.UtcNow));
                return Result.Success;
            });
            return new Fixture { Source = source, Provider = provider, Version = version, CampaignId = campaign.CampaignId };
        }

        public async Task<Result<CampaignAcknowledgementView>> AcknowledgeAsync(Uuid actor, Uuid person)
        {
            await using var scope = Provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
                Source.Acknowledge(CampaignId, person, Version),
                new RequestDispatchContext(ProgramManagementServices.Actor(actor),
                    new HttpInvocation("POST", "/synthetic/policy/acknowledgements", "/synthetic/policy/acknowledgements", "synthetic")),
                CancellationToken.None);
        }

        public Task<PolicyDistributionCampaign> CampaignAsync() => ProgramManagementServices.HydrateAsync(Provider,
            new PolicyDistributionCampaign(Source.TenantId, CampaignId));

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            await Source.Provider.DisposeAsync();
        }
    }

    sealed class Permissions(Uuid manager) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(permission == IProgramReadRequest.ReadPermission || userId == manager);
    }

    sealed class Memberships(Uuid tenant, Uuid manager, Uuid member, string affiliation, string membership)
        : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == tenant.ToString() && (userId == manager || userId == member)
                && membership != "missing" ? new TenantMembershipView(userId, tenant,
                    membership == "firm_staff" ? "firm_staff" : affiliation, IsSuspended: membership == "suspended") : null);
        public async ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            await GetAsync(tenantId, userId, ct) is not null;
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}
