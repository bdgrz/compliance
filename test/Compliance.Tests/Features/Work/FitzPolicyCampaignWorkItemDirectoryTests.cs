using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzPolicyCampaignWorkItemDirectoryTests
{
    static readonly DateOnly Today = new(2026, 10, 5);
    static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldProjectPendingCampaignParticipantAsAccountableWorkGivenCampaignLaunch()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var personId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var writer = sourceScope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var directory = new FitzPolicyCampaignWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        var person = new Person(fixture.TenantId, personId);
        var actor = ActorReference.ForMember(fixture.LeadMemberId, "Compliance Lead");
        Assert.True(person.Record("Ada Lovelace", null, actor, Now).IsSuccess);
        Assert.Null(person.CorrelateMembership(1, userId, actor, Now));
        await writer.SaveAsync(person, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var launched = Launched(fixture, campaignId, 1);
        var audience = new PolicyCampaignAudienceFrozen(fixture.TenantId, campaignId, 1,
        [
            new CampaignAudienceEntry(personId, "Ada Lovelace", "employee", "Engineering",
                Today.AddYears(-1), Today.AddDays(7)),
        ]);

        // Act
        await ProjectAsync(directory, fixture.TenantId, launched, audience);
        var result = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            Today, Today.AddDays(30), null);
        var outsideHorizon = await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, Today, Today.AddDays(6), null);
        var otherProgram = await directory.LoadProgramAsync(fixture.TenantId,
            Uuid.CreateVersion4(), Today, DateOnly.MaxValue, null);
        var otherTenant = await directory.LoadProgramAsync(Uuid.CreateVersion4(),
            fixture.ProgramId, Today, DateOnly.MaxValue, null);

        // Assert
        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value);
        Assert.Equal(PolicyCampaignWork.Acknowledgement, item.Kind);
        Assert.Equal(Uuid.CreateVersion5(campaignId, "participant:" + personId), item.SourceId);
        Assert.Equal(Today.AddDays(7), item.DueOn);
        Assert.Equal("acknowledge", item.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"campaigns/{campaignId}/acknowledgements", item.ActionPath);
        Assert.Equal(new OperatingHolder(OperatingAuthority.MemberHolder,
            RbacIds.Member(fixture.TenantId, userId)), item.Responsible);
        Assert.Equal(Now, item.CreatedAt);
        Assert.Empty(outsideHorizon.Value);
        Assert.Empty(otherProgram.Value);
        Assert.Empty(otherTenant.Value);
        Assert.Equal(2, await directory.LoadRevisionAsync(fixture.TenantId));
        Assert.Equal(FitzPolicyCampaignWorkItemDirectory.ProjectorName,
            ((IAccountableWorkItemDirectoryReader)directory).ProjectorName);
        Assert.Equal(new ProjectionCheckpoint(new EventCursor("campaign-work-cursor")),
            await directory.LoadCheckpointAsync(fixture.TenantId));
    }

    [Fact]
    public async Task ShouldRestoreParticipantWorkGivenWaiverExpiresWithoutFurtherCampaignEvents()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var personId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzPolicyCampaignWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        var launched = Launched(fixture, campaignId, 1);
        var audience = Audience(fixture.TenantId, campaignId, personId);
        var waiver = new CampaignWaiverApproved(fixture.TenantId, campaignId,
            Uuid.CreateVersion4(), personId, "Leave", Today.AddDays(10),
            ActorReference.ForMember(fixture.LeadMemberId, "Compliance Lead"),
            fixture.LeadMemberId, Now);
        await ProjectAsync(directory, fixture.TenantId, launched, audience, waiver);

        // Act
        var covered = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            Today.AddDays(9), DateOnly.MaxValue, null);
        var expired = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            Today.AddDays(10), DateOnly.MaxValue, null);

        // Assert
        Assert.Empty(covered.Value);
        var item = Assert.Single(expired.Value);
        Assert.Equal(Uuid.CreateVersion5(campaignId, "participant:" + personId), item.SourceId);
    }

    [Fact]
    public async Task ShouldKeepAcknowledgementSatisfiedGivenAcknowledgedParticipantLeavesAndRejoins()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var personId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzPolicyCampaignWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        var launched = Launched(fixture, campaignId, 1);
        var audience = Audience(fixture.TenantId, campaignId, personId);
        var actor = ActorReference.ForMember(fixture.LeadMemberId, "Compliance Lead");
        var acknowledgement = new PolicyAcknowledgementRecorded(fixture.TenantId, campaignId,
            Uuid.CreateVersion4(), personId, launched.Subject.RecordId, launched.Subject.Version,
            launched.Subject.ContentSha256, PolicyDistributionCampaign.DefaultAcknowledgementText,
            new ActorReference("workforce_person", personId.ToString(), "Ada"), actor, true, Now);
        var removal = new PolicyCampaignAudienceAmended(fixture.TenantId, campaignId, 1, 1,
            Uuid.CreateVersion4(), "roster-next", Today, [new CampaignAudienceAmendment(personId,
                "Ada Lovelace", "leaver", null, null, null)], actor, Now.AddDays(1));
        var rejoin = new PolicyCampaignAudienceAmended(fixture.TenantId, campaignId, 2, 1,
            Uuid.CreateVersion4(), "roster-later", Today.AddDays(2),
            [new CampaignAudienceAmendment(personId, "Ada Lovelace", "joiner", "employee",
                "Engineering", Today.AddDays(20))], actor, Now.AddDays(2));
        await ProjectAsync(directory, fixture.TenantId, launched, audience, acknowledgement,
            removal, rejoin);

        // Act
        var result = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            Today.AddDays(3), DateOnly.MaxValue, null);

        // Assert
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ShouldRemoveTrainingWorkAfterCompletionIsRecordedGivenTrainingCampaign()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var personId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzPolicyCampaignWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        var launched = Launched(fixture, campaignId, 1) with
        {
            Subject = new CampaignSubject("training", Uuid.CreateVersion4(), "TR-SEC",
                "Security Awareness", 1, "training-hash"),
            AcknowledgementText = null,
        };
        var audience = Audience(fixture.TenantId, campaignId, personId);
        var completed = new TrainingCompletionRecorded(fixture.TenantId, campaignId,
            Uuid.CreateVersion4(), personId, launched.Subject.RecordId, 1, Today,
            "manual", "evidence:completion", ActorReference.ForMember(fixture.LeadMemberId,
                "Compliance Lead"), Now);
        await ProjectAsync(directory, fixture.TenantId, launched, audience, completed);

        // Act
        var result = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            Today, DateOnly.MaxValue, null);

        // Assert
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ShouldRemoveCampaignWorkAfterClosureGivenPendingParticipant()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var personId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzPolicyCampaignWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        var launched = Launched(fixture, campaignId, 1);
        var audience = Audience(fixture.TenantId, campaignId, personId);
        var closed = new PolicyCampaignClosed(fixture.TenantId, campaignId, Today,
            "Period ended", new CampaignTotalsView(1, 1, 0, 0, 1, 0, 0, 0),
            ActorReference.ForMember(fixture.LeadMemberId, "Compliance Lead"), Now);
        await ProjectAsync(directory, fixture.TenantId, launched, audience, closed);

        // Act
        var result = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            Today, DateOnly.MaxValue, null);

        // Assert
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ShouldListProjectedCampaignWithoutDirectCampaignReadGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var campaignId = Uuid.CreateVersion4();
        var personId = Uuid.CreateVersion4();
        var candidate = PolicyCampaignWork.CreateCandidate(fixture.TenantId, fixture.ProgramId,
            campaignId, new CampaignSubject("policy", Uuid.CreateVersion4(), "POL-AC",
                "Access Control Policy", 2, "content-hash"), fixture.ApproverMemberId, Now,
            personId, "Ada Lovelace", Today.AddDays(7), fixture.ApproverMemberId);
        var projected = new CampaignProjection(candidate);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events, [projected]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            campaigns: new UnexpectedCampaignDirectory(), accountableWorkItems: [projected]);
        var actor = new OperationsActor(fixture.ApproverUserId, fixture.ApproverMemberId,
            "Approver");

        // Act
        var result = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Entries,
            entry => entry.Candidate.Kind == PolicyCampaignWork.Acknowledgement);
    }

    static PolicyCampaignLaunched Launched(OperationsFixture fixture, Uuid campaignId,
        int audienceCount) => new(fixture.TenantId, fixture.ProgramId, campaignId,
        new CampaignSubject("policy", Uuid.CreateVersion4(), "POL-AC", "Access Control Policy",
            2, "content-hash"), PolicyAudience.CoreSecurity, [], Uuid.CreateVersion4(),
        "roster-hash", Today, Today.AddDays(7), "Read and acknowledge",
        PolicyDistributionCampaign.DefaultAcknowledgementText, audienceCount,
        ActorReference.ForMember(fixture.LeadMemberId, "Compliance Lead"),
        fixture.LeadMemberId, Now);

    static PolicyCampaignAudienceFrozen Audience(Uuid tenantId, Uuid campaignId, Uuid personId) =>
        new(tenantId, campaignId, 1,
        [new CampaignAudienceEntry(personId, "Ada Lovelace", "employee", "Engineering",
            Today.AddYears(-1), Today.AddDays(7))]);

    static async Task ProjectAsync(FitzPolicyCampaignWorkItemDirectory directory,
        Uuid tenantId, params DomainEvent[] events)
    {
        var identity = new CheckpointIdentity(FitzPolicyCampaignWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "policy-distribution-campaigns"));
        var cursor = new ProjectionCheckpoint(new EventCursor("campaign-work-cursor"));
        await using var batch = await directory.BeginAsync(
            new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(cursor);
    }

    sealed class CampaignProjection(WorkCandidate candidate) : IAccountableWorkItemDirectoryReader
    {
        public string ProjectorName => "TestPolicyCampaignWorkItems";

        public IReadOnlyCollection<string> ProjectedKinds => PolicyCampaignWork.ProjectedKinds;

        public EventStreamPattern SourcePattern(Uuid tenantId) =>
            EventStreamPattern.ForPattern(tenantId.ToString(), "policy-distribution-campaigns");

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
            Uuid programId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<IReadOnlyList<WorkCandidate>>.Success([candidate]));

        public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
            Uuid programId, DateOnly today, DateOnly horizon, Uuid? workItemId,
            CancellationToken ct = default) =>
            ValueTask.FromResult(Result<IReadOnlyList<WorkCandidate>>.Success(
                candidate.DueOn is { } dueOn && dueOn > horizon ||
                workItemId is { } wanted && candidate.WorkItemId != wanted ? [] : [candidate]));
    }

    sealed class UnexpectedCampaignDirectory : ICampaignDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => throw new InvalidOperationException(
            "The campaign source directory should not be read when all campaign work is projected.");

        public ValueTask<Page<CampaignSummaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            throw new InvalidOperationException(
                "The campaign source directory should not be read when all campaign work is projected.");

        public ValueTask<IReadOnlyList<Uuid>> ListForSubjectAsync(Uuid tenantId, Uuid programId,
            Uuid subjectId, long version, CancellationToken ct = default) =>
            throw new InvalidOperationException(
                "The campaign source directory should not be read when all campaign work is projected.");
    }
}
