using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class TrainingWorkAuthorityTests
{
    [Fact]
    public async Task ShouldRouteRecordingWorkToManagerGivenCorrelatedAudienceMemberCannotRecordCompletion()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Completion();

        // Act
        await fixture.Scenario(fixture.LearnerUserId).When(request)
            .ExpectFailure(RequestErrorKind.Forbidden);
        var owner = await fixture.QueueAsync(fixture.OwnerUserId);
        var learner = await fixture.QueueAsync(fixture.LearnerUserId);
        var item = Assert.Single(owner.Items);
        var detail = await fixture.Scenario(fixture.OwnerUserId).When(new GetWorkItem(
            fixture.TenantId, fixture.ProgramId, item.WorkItemId)).ExpectSuccess();
        await fixture.Scenario(fixture.LearnerUserId).When(new GetWorkItem(
            fixture.TenantId, fixture.ProgramId, item.WorkItemId)).ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.Equal(item, detail.Value.Item);
        Assert.Equal(PolicyCampaignWork.TrainingCompletion, item.Kind);
        Assert.Equal("record_completion", item.NextAction);
        Assert.Equal(fixture.OwnerMemberId, item.AssigneeMemberId);
        Assert.Empty(learner.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), learner.Counts);
    }

    [Fact]
    public async Task ShouldRemoveRecordingWorkGivenManagerRecordsActualSourceCompletion()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.QueueAsync(fixture.OwnerUserId);

        // Act
        var completion = await fixture.Scenario(fixture.OwnerUserId)
            .When(fixture.Completion()).ExpectSuccess();
        await fixture.CatchUpAsync();
        var after = await fixture.QueueAsync(fixture.OwnerUserId);

        // Assert
        Assert.Single(before.Items);
        Assert.Equal(fixture.PersonId, completion.Value.PersonId);
        Assert.Equal("manual", completion.Value.Source);
        Assert.Empty(after.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), after.Counts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldExposeUnassignedRecordingWorkGivenOwnerAuthorityRemoved(bool deprovision)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        if (deprovision)
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new Member(fixture.TenantId, fixture.OwnerUserId), member => member.Deprovision(
                    fixture.OtherManagerMemberId, "Other manager", DateTimeOffset.UtcNow, "Left."));
        else
            fixture.Permissions.Managers.Remove(fixture.OwnerMemberId);

        // Act
        var queue = await fixture.QueueAsync(fixture.OtherManagerUserId, "unassigned");

        // Assert
        var item = Assert.Single(queue.Items);
        Assert.Null(item.AssigneeMemberId);
        Assert.Equal("record_completion", item.NextAction);
        Assert.Equal(1, queue.Counts.Total);
    }

    [Fact]
    public async Task ShouldRejectTrainingAssignmentGivenAudienceMemberLacksSourceRecordingAuthority()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var queue = await fixture.QueueAsync(fixture.OwnerUserId, "all");
        var item = Assert.Single(queue.Items);

        // Act
        var result = await fixture.Scenario(fixture.OwnerUserId).When(new AssignWorkItem(
            fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0, fixture.LearnerMemberId))
            .ExpectFailure(RequestErrorKind.Validation);

        // Assert
        Assert.Contains("source workflow", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldDelegateRecordingToAnotherManagerGivenCurrentSourcePermission()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var item = Assert.Single((await fixture.QueueAsync(fixture.OwnerUserId)).Items);

        // Act
        var assigned = await fixture.Scenario(fixture.OwnerUserId).When(new DelegateWorkItem(
            fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0,
            fixture.OtherManagerMemberId, "Another manager reviews the proof."))
            .ExpectSuccess();
        var delegated = await fixture.QueueAsync(fixture.OtherManagerUserId);
        await fixture.Scenario(fixture.OtherManagerUserId).When(fixture.Completion()).ExpectSuccess();
        await fixture.CatchUpAsync();
        var after = await fixture.QueueAsync(fixture.OtherManagerUserId);

        // Assert
        Assert.Equal(fixture.OtherManagerMemberId, assigned.Value.Item.AssigneeMemberId);
        Assert.Equal(item.WorkItemId, Assert.Single(delegated.Items).WorkItemId);
        Assert.Empty(after.Items);
    }

    [Fact]
    public async Task ShouldPreservePersonalPolicyAcknowledgementGivenCorrelatedAudienceMember()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("policy");
        var before = await fixture.QueueAsync(fixture.LearnerUserId);
        var item = Assert.Single(before.Items);

        // Act
        var recorded = await fixture.Scenario(fixture.LearnerUserId).When(new AcknowledgePolicy(
            fixture.TenantId, fixture.ProgramId, fixture.CampaignId, fixture.PersonId, 1,
            "subject-hash", PolicyDistributionCampaign.DefaultAcknowledgementText)).ExpectSuccess();
        await fixture.CatchUpAsync();
        var after = await fixture.QueueAsync(fixture.LearnerUserId);

        // Assert
        Assert.Equal("acknowledge", item.NextAction);
        Assert.Equal(fixture.LearnerMemberId, item.AssigneeMemberId);
        Assert.False(recorded.Value.RecordedOnBehalf);
        Assert.Empty(after.Items);
    }

    [Fact]
    public async Task ShouldPreservePersonalAcknowledgementAndClaimContractGivenRetainedAttestHistory()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("policy");
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.TenantId,
            fixture.LearnerUserId, revoked: true);
        var before = await fixture.QueueAsync(fixture.LearnerUserId);
        var item = Assert.Single(before.Items);

        // Act
        await fixture.Scenario(fixture.LearnerUserId).When(new GetWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId)).ExpectSuccess();
        var claim = await fixture.Scenario(fixture.LearnerUserId).When(new ClaimWorkItem(
            fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0))
            .ExpectFailure(RequestErrorKind.Validation);
        var recorded = await fixture.Scenario(fixture.LearnerUserId).When(new AcknowledgePolicy(
            fixture.TenantId, fixture.ProgramId, fixture.CampaignId, fixture.PersonId, 1,
            "subject-hash", PolicyDistributionCampaign.DefaultAcknowledgementText)).ExpectSuccess();
        await fixture.CatchUpAsync();
        var after = await fixture.QueueAsync(fixture.LearnerUserId);

        // Assert
        Assert.Equal("acknowledge", item.NextAction);
        Assert.Equal(fixture.LearnerMemberId, item.AssigneeMemberId);
        Assert.Equal(1, before.Counts.Total);
        Assert.Contains("Only team work", claim.Error!.Message, StringComparison.Ordinal);
        Assert.False(recorded.Value.RecordedOnBehalf);
        Assert.Empty(after.Items);
    }

    [Fact]
    public async Task ShouldPreservePolicyOrphanGivenStillCorrelatedDeprovisionedAudienceMember()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("policy");
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new Member(fixture.TenantId, fixture.LearnerUserId), member => member.Deprovision(
                fixture.OwnerMemberId, "Owner", DateTimeOffset.UtcNow, "Left the organization."));

        // Act
        var orphan = await fixture.QueueAsync(fixture.OwnerUserId, "all");
        await fixture.Scenario(fixture.OwnerUserId).When(new AcknowledgePolicy(
            fixture.TenantId, fixture.ProgramId, fixture.CampaignId, fixture.PersonId, 1,
            "subject-hash", PolicyDistributionCampaign.DefaultAcknowledgementText))
            .ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        var item = Assert.Single(orphan.Items);
        Assert.Null(item.AssigneeMemberId);
        Assert.Equal("acknowledge", item.NextAction);
        Assert.Equal(new OperatingHolder(OperatingAuthority.MemberHolder, fixture.LearnerMemberId),
            item.Responsible);
    }

    [Fact]
    public async Task ShouldHideOnBehalfRecordingGivenCampaignOwnerLacksSourceAuthority()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("policy", correlated: false);
        var previous = Assert.Single((await fixture.QueueAsync(fixture.OwnerUserId)).Items);
        fixture.Permissions.Managers.Remove(fixture.OwnerMemberId);

        // Act
        await fixture.Scenario(fixture.OwnerUserId).When(new AcknowledgePolicy(
            fixture.TenantId, fixture.ProgramId, fixture.CampaignId, fixture.PersonId, 1,
            "subject-hash", PolicyDistributionCampaign.DefaultAcknowledgementText))
            .ExpectFailure(RequestErrorKind.NotFound);
        var owner = await fixture.QueueAsync(fixture.OwnerUserId);
        var manager = await fixture.QueueAsync(fixture.OtherManagerUserId, "unassigned");
        await fixture.Scenario(fixture.OwnerUserId).When(new GetWorkItem(
            fixture.TenantId, fixture.ProgramId, previous.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);
        var detail = await fixture.Scenario(fixture.OtherManagerUserId).When(new GetWorkItem(
            fixture.TenantId, fixture.ProgramId, previous.WorkItemId)).ExpectSuccess();

        // Assert
        Assert.Null(detail.Value.Item.AssigneeMemberId);
        Assert.Empty(owner.Items);
        var item = Assert.Single(manager.Items);
        Assert.Null(item.AssigneeMemberId);
        Assert.Equal("record_acknowledgement", item.NextAction);
        Assert.Equal(1, manager.Counts.Total);
    }

    [Fact]
    public async Task ShouldRemoveOnBehalfRecordingGivenEligibleManagerRecordsSourceAcknowledgement()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("policy", correlated: false);
        var item = Assert.Single((await fixture.QueueAsync(fixture.OwnerUserId)).Items);

        // Act
        var recorded = await fixture.Scenario(fixture.OwnerUserId).When(new AcknowledgePolicy(
            fixture.TenantId, fixture.ProgramId, fixture.CampaignId, fixture.PersonId, 1,
            "subject-hash", PolicyDistributionCampaign.DefaultAcknowledgementText)).ExpectSuccess();
        await fixture.CatchUpAsync();
        var after = await fixture.QueueAsync(fixture.OwnerUserId);

        // Assert
        Assert.Equal("record_acknowledgement", item.NextAction);
        Assert.True(recorded.Value.RecordedOnBehalf);
        Assert.Empty(after.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), after.Counts);
    }

    [Fact]
    public async Task ShouldRejectOnBehalfAssignmentGivenAssigneeLacksSourceRecordingAuthority()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("policy", correlated: false);
        var item = Assert.Single((await fixture.QueueAsync(fixture.OwnerUserId)).Items);

        // Act
        var result = await fixture.Scenario(fixture.OwnerUserId).When(new AssignWorkItem(
            fixture.TenantId, fixture.ProgramId, item.WorkItemId, 0, fixture.LearnerMemberId))
            .ExpectFailure(RequestErrorKind.Validation);

        // Assert
        Assert.Contains("source workflow", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("training")]
    [InlineData("policy")]
    public async Task ShouldRouteRecordingToGuestManagerGivenSourceExplicitlyAuthorizesGuest(string kind)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(kind, correlated: false,
            ownerAffiliation: "guest");
        var before = await fixture.QueueAsync(fixture.OwnerUserId);

        // Act
        if (kind == "training")
            await fixture.Scenario(fixture.OwnerUserId).When(fixture.Completion()).ExpectSuccess();
        else
            await fixture.Scenario(fixture.OwnerUserId).When(new AcknowledgePolicy(
                fixture.TenantId, fixture.ProgramId, fixture.CampaignId, fixture.PersonId, 1,
                "subject-hash", PolicyDistributionCampaign.DefaultAcknowledgementText)).ExpectSuccess();
        await fixture.CatchUpAsync();
        var after = await fixture.QueueAsync(fixture.OwnerUserId);

        // Assert
        Assert.Equal(fixture.OwnerMemberId, Assert.Single(before.Items).AssigneeMemberId);
        Assert.Empty(after.Items);
    }

    sealed class MembershipDirectory(IAggregateReader reader) : ITenantMembershipDirectoryReader
    {
        public async ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default)
        {
            var id = Uuid.Parse(tenantId, System.Globalization.CultureInfo.InvariantCulture);
            var member = await reader.HydrateAsync(new Member(id, userId), ct);
            return member.IsRegistered || member.IsDeprovisioned
                ? new TenantMembershipView(userId, id, member.Affiliation!, member.IsSuspended,
                    IsDeprovisioned: member.IsDeprovisioned)
                : null;
        }

        public async ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => await GetAsync(tenantId, userId, ct) is
            { IsSuspended: false, IsDeprovisioned: false };

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class Fixture : IAsyncDisposable
    {
        public required ServiceProvider Provider { get; init; }
        public required OperationsFixture.ManagerPermissions Permissions { get; init; }
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid CampaignId { get; } = Uuid.CreateVersion4();
        public Uuid PersonId { get; } = Uuid.CreateVersion4();
        public Uuid OwnerUserId { get; } = Uuid.CreateVersion4();
        public Uuid LearnerUserId { get; } = Uuid.CreateVersion4();
        public Uuid OtherManagerUserId { get; } = Uuid.CreateVersion4();
        public Uuid OwnerMemberId => RbacIds.Member(TenantId, OwnerUserId);
        public Uuid LearnerMemberId => RbacIds.Member(TenantId, LearnerUserId);
        public Uuid OtherManagerMemberId => RbacIds.Member(TenantId, OtherManagerUserId);
        public DateOnly Today { get; } = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        public static async Task<Fixture> CreateAsync(string kind = "training", bool correlated = true,
            string ownerAffiliation = "client_personnel")
        {
            var permissions = new OperationsFixture.ManagerPermissions();
            var client = new InMemoryKvClient();
            var provider = ProgramManagementServices.Build(permissions, portia => portia
                    .AddRequestHandler<RecordTrainingCompletionHandler>()
                    .AddRequestHandler<AcknowledgePolicyHandler>()
                    .AddRequestHandler<ListWorkHandler>()
                    .AddRequestHandler<GetWorkItemHandler>()
                    .AddRequestHandler<AssignWorkItemHandler>()
                    .AddRequestHandler<ClaimWorkItemHandler>()
                    .AddRequestHandler<DelegateWorkItemHandler>(),
                services =>
                {
                    services.AddScoped<ITenantMembershipDirectoryReader, MembershipDirectory>();
                    services.AddScoped<OperatingAuthority>();
                    services.AddScoped<WorkQueueReader>();
                    services.AddScoped<WorkQueueReadConsistency>();
                    services.AddSingleton<IDomainEventReader>(provider =>
                        (IDomainEventReader)provider.GetRequiredService<IEventStore>());
                    services.AddScoped<FitzPolicyCampaignWorkItemDirectory>(provider => new(client,
                        provider.GetRequiredService<IAggregateReader>()));
                    services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
                        provider.GetRequiredService<FitzPolicyCampaignWorkItemDirectory>());
                });
            var fixture = new Fixture { Provider = provider, Permissions = permissions };
            permissions.Managers.UnionWith([fixture.OwnerMemberId, fixture.OtherManagerMemberId]);
            foreach (var user in new[] { fixture.OwnerUserId, fixture.OtherManagerUserId,
                         fixture.LearnerUserId })
            {
                if (user == fixture.OwnerUserId && ownerAffiliation == "guest")
                {
                    DomainEvent registered = new MemberRegistered(fixture.TenantId,
                        fixture.OwnerMemberId, user, ownerAffiliation, Uuid.CreateVersion4());
                    registered.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
                        fixture.OwnerMemberId, 1, DateTimeOffset.UtcNow));
                    await provider.GetRequiredService<IEventStore>().AppendAsync(
                        new EventStreamAddress(fixture.TenantId.ToString(), "rbac-members",
                            fixture.OwnerMemberId.ToString()), 0, [registered]);
                }
                else
                    await ProgramManagementServices.SeedAsync(provider, new Member(fixture.TenantId,
                        user), member => member.Register(ownerAffiliation == "firm_staff" &&
                            user == fixture.OwnerUserId ? ownerAffiliation : "client_personnel"));
            }
            var now = DateTimeOffset.UtcNow;
            var actor = ActorReference.ForMember(fixture.OwnerMemberId, "Campaign owner");
            await ProgramManagementServices.SeedAsync(provider, new Person(fixture.TenantId,
                fixture.PersonId), person =>
                {
                    Assert.True(person.Record("Learner", null, actor, now).IsSuccess);
                    if (correlated)
                        Assert.Null(person.CorrelateMembership(1, fixture.LearnerUserId, actor, now));
                    return Result.Success;
                });
            var audience = new RosterAudience(new Dictionary<Uuid, RosterMatch>
            {
                [fixture.PersonId] = new(fixture.PersonId, "Learner", "employee", "Engineering",
                    fixture.Today.AddYears(-1)),
            }, new HashSet<Uuid> { fixture.PersonId });
            await ProgramManagementServices.SeedAsync<PolicyDistributionCampaign, CampaignRegistration>(
                provider, new PolicyDistributionCampaign(fixture.TenantId, fixture.CampaignId),
                campaign => campaign.Launch(fixture.ProgramId,
                    new CampaignSubject(kind, Uuid.CreateVersion4(), "SEC-1", "Security awareness",
                        1, "subject-hash"), PolicyAudience.CoreSecurity, [], Uuid.CreateVersion4(),
                    "roster-hash", audience, fixture.Today, "Collect attributable evidence.",
                    actor, fixture.OwnerMemberId, now));
            await fixture.CatchUpAsync();
            return fixture;
        }

        public RecordTrainingCompletion Completion() => new(TenantId, ProgramId, CampaignId,
            PersonId, 1, Today, "manual", "certificate/security-awareness.pdf");

        public RequestScenario Scenario(Uuid userId) => RequestScenario.For(Provider)
            .GivenActor(ProgramManagementServices.Actor(userId));

        public async Task<WorkQueueView> QueueAsync(Uuid userId, string scope = "mine") =>
            (await Scenario(userId).When(new ListWork(TenantId, ProgramId, scope))
                .ExpectSuccess()).Value;

        public async Task CatchUpAsync()
        {
            await using var scope = Provider.CreateAsyncScope();
            var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
            var directory = scope.ServiceProvider.GetRequiredService<FitzPolicyCampaignWorkItemDirectory>();
            var checkpoint = await directory.LoadCheckpointAsync(TenantId);
            var pattern = directory.SourcePattern(TenantId);
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity(FitzPolicyCampaignWorkItemDirectory.ProjectorName, pattern), checkpoint));
            var cursor = checkpoint.Cursor;
            await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
            {
                await directory.ApplyAsync(record.Event);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }
}
