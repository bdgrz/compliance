using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution;

public sealed class PolicyCampaignHandlerTests
{
    static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task ShouldRejectSuccessorApprovalGivenUnprojectedPredecessorCampaign()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var predecessor = await fixture.ApprovePolicyAsync();
        var successor = await fixture.Scenario(fixture.AuthorUserId)
            .When(new ProposePolicySuccessor(fixture.TenantId, fixture.ProgramId,
                predecessor.PolicyId, 1, predecessor.Content with { Title = "Successor" }))
            .ExpectSuccess();
        var review = await fixture.Scenario(fixture.ReviewerUserId)
            .When(new ReviewPolicyDraft(fixture.TenantId, fixture.ProgramId,
                predecessor.PolicyId, successor.Value.Revision, "accept", "Reviewed"))
            .ExpectSuccess();
        var preview = await fixture.Scenario(fixture.ManagerUserId)
            .When(new PreviewPolicyImpact(fixture.TenantId, fixture.ProgramId,
                predecessor.PolicyId, successor.Value.Revision)).ExpectSuccess();
        var campaign = await fixture.LaunchAsync(predecessor);

        // Act
        var approval = await fixture.Scenario(fixture.ManagerUserId)
            .When(new ApprovePolicy(fixture.TenantId, fixture.ProgramId, predecessor.PolicyId,
                successor.Value.Revision, review.Value.DecisionId,
                new DateOnly(2026, 11, 1), true, "Approved", preview.Value.Digest))
            .ExpectFailure(RequestErrorKind.Conflict);
        var retained = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new Policy(fixture.TenantId, predecessor.PolicyId));

        // Assert
        Assert.True(approval.Error!.IsTransient);
        Assert.Equal(1, retained.CurrentVersion!.Version);
        Assert.NotNull(retained.DraftContent);

        // Act: the projector catches up; an old digest still cannot acknowledge the new impact.
        await fixture.ProjectCampaignsAsync();
        await fixture.Scenario(fixture.ManagerUserId)
            .When(new ApprovePolicy(fixture.TenantId, fixture.ProgramId, predecessor.PolicyId,
                successor.Value.Revision, review.Value.DecisionId,
                new DateOnly(2026, 11, 1), true, "Approved", preview.Value.Digest))
            .ExpectFailure(RequestErrorKind.Conflict);
        var refreshed = await fixture.Scenario(fixture.ManagerUserId)
            .When(new PreviewPolicyImpact(fixture.TenantId, fixture.ProgramId,
                predecessor.PolicyId, successor.Value.Revision)).ExpectSuccess();
        var approved = await fixture.Scenario(fixture.ManagerUserId)
            .When(new ApprovePolicy(fixture.TenantId, fixture.ProgramId, predecessor.PolicyId,
                successor.Value.Revision, review.Value.DecisionId,
                new DateOnly(2026, 11, 1), true, "Approved", refreshed.Value.Digest))
            .ExpectSuccess();

        // Assert
        Assert.Equal([campaign.CampaignId], refreshed.Value.AffectedCampaignIds);
        Assert.Equal(2, approved.Value.Version);
    }

    [Fact]
    public async Task ShouldApproveExactVersionAndLaunchFrozenAudienceGivenIndependentMembers()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();

        // Act
        var version = await fixture.ApprovePolicyAsync();
        var campaign = await fixture.LaunchAsync(version);

        // Assert
        Assert.Equal(1, version.Version);
        Assert.Equal(2, campaign.AudienceCount);
        var view = await fixture.Scenario(fixture.MemberUserId)
            .When(new GetCampaign(fixture.TenantId, fixture.ProgramId, campaign.CampaignId))
            .ExpectSuccess();
        Assert.Equal(version.ContentSha256, view.Value.Subject.ContentSha256);
        Assert.Equal(fixture.RosterSnapshotId, view.Value.RosterSnapshotId);
        Assert.Equal(2, view.Value.Totals.Pending);
    }

    [Fact]
    public async Task ShouldAttributePersonalAndRecordedAcknowledgementsGivenMemberAndNonMember()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var version = await fixture.ApprovePolicyAsync();
        var campaign = await fixture.LaunchAsync(version);

        // Act
        var memberForOther = await fixture.Scenario(fixture.MemberUserId)
            .When(fixture.Acknowledge(campaign.CampaignId, fixture.NonMemberPersonId, version))
            .ExpectFailure(RequestErrorKind.NotFound);
        var managerForMember = await fixture.Scenario(fixture.ManagerUserId)
            .When(fixture.Acknowledge(campaign.CampaignId, fixture.MemberPersonId, version))
            .ExpectFailure(RequestErrorKind.Forbidden);
        var personal = await fixture.Scenario(fixture.MemberUserId)
            .When(fixture.Acknowledge(campaign.CampaignId, fixture.MemberPersonId, version))
            .ExpectSuccess();
        var recorded = await fixture.Scenario(fixture.ManagerUserId)
            .When(fixture.Acknowledge(campaign.CampaignId, fixture.NonMemberPersonId, version))
            .ExpectSuccess();
        var wrongVersion = await fixture.Scenario(fixture.MemberUserId)
            .When(fixture.Acknowledge(campaign.CampaignId, fixture.MemberPersonId, version) with
            {
                PolicyVersion = 2,
            })
            .ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.False(memberForOther.IsSuccess);
        Assert.False(managerForMember.IsSuccess);
        Assert.False(wrongVersion.IsSuccess);
        Assert.False(personal.Value.RecordedOnBehalf);
        Assert.Equal(fixture.MemberPersonId.ToString(), personal.Value.Performer.Id);
        Assert.True(recorded.Value.RecordedOnBehalf);
        Assert.Equal("workforce_person", recorded.Value.Performer.Kind);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.ManagerUserId).ToString(),
            recorded.Value.Recorder.Id);
        var totals = (await fixture.Scenario(fixture.MemberUserId)
            .When(new GetCampaign(fixture.TenantId, fixture.ProgramId, campaign.CampaignId,
                DateOnly.FromDateTime(Now.UtcDateTime).AddDays(1)))
            .ExpectSuccess()).Value.Totals;
        Assert.Equal(2, totals.Satisfied);
    }

    [Fact]
    public async Task ShouldNotDiscloseAudienceMembershipGivenMemberAcknowledgingForOthers()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var version = await fixture.ApprovePolicyAsync();
        var campaign = await fixture.LaunchAsync(version);

        // Act
        var inAudience = await fixture.Scenario(fixture.MemberUserId)
            .When(fixture.Acknowledge(campaign.CampaignId, fixture.NonMemberPersonId, version))
            .ExpectFailure(RequestErrorKind.NotFound);
        var outsideAudience = await fixture.Scenario(fixture.MemberUserId)
            .When(fixture.Acknowledge(campaign.CampaignId, Uuid.CreateVersion4(), version))
            .ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.Equal(outsideAudience.Error!.Message, inAudience.Error!.Message);
    }

    [Fact]
    public async Task ShouldRestrictPersonDetailAndWaiversGivenReadOnlyMember()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var version = await fixture.ApprovePolicyAsync();
        var campaign = await fixture.LaunchAsync(version);
        var expires = DateOnly.FromDateTime(Now.UtcDateTime).AddMonths(3);

        // Act
        await fixture.Scenario(fixture.MemberUserId)
            .When(new ListCampaignParticipants(fixture.TenantId, fixture.ProgramId,
                campaign.CampaignId))
            .ExpectFailure(RequestErrorKind.Forbidden);
        await fixture.Scenario(fixture.MemberUserId)
            .When(new ApproveCampaignWaiver(fixture.TenantId, fixture.ProgramId,
                campaign.CampaignId, fixture.NonMemberPersonId, "Leave", expires))
            .ExpectFailure(RequestErrorKind.Forbidden);
        var waiver = await fixture.Scenario(fixture.ManagerUserId)
            .When(new ApproveCampaignWaiver(fixture.TenantId, fixture.ProgramId,
                campaign.CampaignId, fixture.NonMemberPersonId, "Extended leave", expires))
            .ExpectSuccess();
        var people = await fixture.Scenario(fixture.ManagerUserId)
            .When(new ListCampaignParticipants(fixture.TenantId, fixture.ProgramId,
                campaign.CampaignId, State: "excepted"))
            .ExpectSuccess();
        await fixture.Scenario(fixture.ManagerUserId)
            .When(new GetCampaign(fixture.TenantId, Uuid.CreateVersion4(), campaign.CampaignId))
            .ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.Equal(expires, waiver.Value.ExpiresOn);
        Assert.Equal(fixture.NonMemberPersonId, Assert.Single(people.Value.Items).PersonId);
    }

    [Fact]
    public async Task ShouldRejectLaunchGivenSupersededVersionOrMissingRoster()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var version = await fixture.ApprovePolicyAsync();

        // Act
        await fixture.Scenario(fixture.ManagerUserId)
            .When(new LaunchPolicyCampaign(fixture.TenantId, fixture.ProgramId, version.PolicyId,
                2, fixture.RosterSnapshotId, DateOnly.FromDateTime(Now.UtcDateTime).AddDays(30)))
            .ExpectFailure(RequestErrorKind.NotFound);
        await fixture.Scenario(fixture.ManagerUserId)
            .When(new LaunchPolicyCampaign(fixture.TenantId, fixture.ProgramId, version.PolicyId,
                1, Uuid.CreateVersion4(), DateOnly.FromDateTime(Now.UtcDateTime).AddDays(30)))
            .ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.Equal("approved", version.Status);
    }

    sealed class Fixture
    {
        public required ServiceProvider Provider { get; init; }
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid AuthorUserId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerUserId { get; } = Uuid.CreateVersion4();
        public Uuid ManagerUserId { get; } = Uuid.CreateVersion4();
        public Uuid MemberUserId { get; } = Uuid.CreateVersion4();
        public Uuid MemberPersonId { get; } = Uuid.CreateVersion4();
        public Uuid NonMemberPersonId { get; } = Uuid.CreateVersion4();
        public Uuid RosterSnapshotId { get; } = Uuid.CreateVersion4();

        public static async Task<Fixture> CreateAsync()
        {
            var managers = new HashSet<Uuid>();
            var provider = ProgramManagementServices.Build(new ManagerPermissions(managers),
                portia => portia.AddRequestHandler<CreatePolicyDraftHandler>()
                    .AddRequestHandler<ProposePolicySuccessorHandler>()
                    .AddRequestHandler<PreviewPolicyImpactHandler>()
                    .AddRequestHandler<ReviewPolicyDraftHandler>()
                    .AddRequestHandler<ApprovePolicyHandler>()
                    .AddRequestHandler<LaunchPolicyCampaignHandler>()
                    .AddRequestHandler<AcknowledgePolicyHandler>()
                    .AddRequestHandler<ApproveCampaignWaiverHandler>()
                    .AddRequestHandler<GetCampaignHandler>()
                    .AddRequestHandler<ListCampaignParticipantsHandler>(),
                services => services.AddScoped<PolicyImpactService>()
                    .AddSingleton<IDomainEventReader>(provider =>
                        (IDomainEventReader)provider.GetRequiredService<IEventStore>())
                    .AddSingleton<ICampaignDirectoryReader>(
                        new FitzCampaignDirectory(new InMemoryKvClient())));
            var fixture = new Fixture { Provider = provider };
            managers.UnionWith([fixture.AuthorUserId, fixture.ReviewerUserId,
                fixture.ManagerUserId]);
            var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Admin");
            var member = new PersonView(fixture.TenantId, fixture.MemberPersonId, 2, "Mia Member",
                null, "manual", actor, Now, fixture.MemberUserId);
            var outsider = new PersonView(fixture.TenantId, fixture.NonMemberPersonId, 1,
                "Nate Nonmember", null, "manual", actor, Now);
            await ProgramManagementServices.SeedAsync(provider,
                new Person(fixture.TenantId, fixture.MemberPersonId), person =>
                {
                    Assert.True(person.Record(member.DisplayName, null, actor, Now).IsSuccess);
                    return Command(person.CorrelateMembership(1, fixture.MemberUserId, actor, Now));
                });
            var rows = WorkforceRosterSnapshotContent.Rows([member, outsider],
                [Job(fixture.TenantId, member.PersonId, "E-1"),
                    Job(fixture.TenantId, outsider.PersonId, "E-2")]);
            var digest = PopulationContentIdentity.Compute(WorkforceRosterSnapshotContent.Kind,
                rows).Value;
            await ProgramManagementServices.SeedAsync(provider,
                new PopulationSnapshot(fixture.TenantId, fixture.RosterSnapshotId), snapshot =>
                    snapshot.Freeze(fixture.RosterSnapshotId, null,
                        WorkforceRosterSnapshotContent.Kind, rows, digest.Sha256, null, actor, Now));
            return fixture;
        }

        static WorkRelationshipView Job(Uuid tenantId, Uuid personId, string workerId) =>
            new(tenantId, WorkRelationship.IdFor(tenantId, workerId), 1, personId, workerId,
                "employee", "active", new DateOnly(2025, 1, 6), null, "Engineering", null, null,
                false, "manual", ActorReference.ForMember(Uuid.CreateVersion4(), "Admin"), Now);

        static Result Command(CommandFailure? failure) => failure is null
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict, failure.Message!));

        public RequestScenario Scenario(Uuid userId) => RequestScenario.For(Provider)
            .GivenActor(ProgramManagementServices.Actor(userId));

        public async Task ProjectCampaignsAsync()
        {
            var events = Provider.GetRequiredService<IDomainEventReader>();
            var directory = (FitzCampaignDirectory)Provider.GetRequiredService<ICampaignDirectoryReader>();
            var pattern = EventStreamPattern.ForPattern(TenantId.ToString(), "policy-distribution-campaigns");
            var checkpoint = await directory.LoadCheckpointAsync(TenantId);
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity(FitzCampaignDirectory.ProjectorName, pattern), checkpoint));
            var cursor = checkpoint.Cursor;
            await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
            {
                await directory.ApplyAsync(record.Event);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }

        public async Task<PolicyVersionView> ApprovePolicyAsync()
        {
            var registration = await Scenario(AuthorUserId).When(new CreatePolicyDraft(TenantId,
                ProgramId, "POL-AC", new PolicyContent("Access Control Policy", "Govern access",
                    PolicyAudience.CoreSecurity, null, 12, "All access is reviewed.", null,
                    "Security lead", []))).ExpectSuccess();
            var review = await Scenario(ReviewerUserId).When(new ReviewPolicyDraft(TenantId,
                ProgramId, registration.Value.PolicyId, 1, "accept", "Accurate"))
                .ExpectSuccess();
            await Scenario(AuthorUserId).When(new ApprovePolicy(TenantId, ProgramId,
                    registration.Value.PolicyId, 1, review.Value.DecisionId,
                    new DateOnly(2026, 10, 1), true, "Self approval"))
                .ExpectFailure(RequestErrorKind.Forbidden);
            var approved = await Scenario(ManagerUserId).When(new ApprovePolicy(TenantId,
                ProgramId, registration.Value.PolicyId, 1, review.Value.DecisionId,
                new DateOnly(2026, 10, 1), true, "Approved")).ExpectSuccess();
            return approved.Value;
        }

        public async Task<CampaignRegistration> LaunchAsync(PolicyVersionView version)
        {
            var launched = await Scenario(ManagerUserId).When(new LaunchPolicyCampaign(TenantId,
                ProgramId, version.PolicyId, version.Version, RosterSnapshotId,
                DateOnly.FromDateTime(Now.UtcDateTime).AddDays(30))).ExpectSuccess();
            return launched.Value;
        }

        public AcknowledgePolicy Acknowledge(Uuid campaignId, Uuid personId,
            PolicyVersionView version) => new(TenantId, ProgramId, campaignId, personId,
            version.Version, version.ContentSha256,
            PolicyDistributionCampaign.DefaultAcknowledgementText);
    }

    sealed class ManagerPermissions(IReadOnlySet<Uuid> managers) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(
            permission == IProgramReadRequest.ReadPermission || managers.Contains(userId));
    }
}
