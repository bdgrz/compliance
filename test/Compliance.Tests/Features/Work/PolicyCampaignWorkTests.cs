using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class PolicyCampaignWorkTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid OwnerMemberId = Uuid.CreateVersion4();
    static readonly Uuid Ada = Uuid.CreateVersion4();
    static readonly Uuid Bob = Uuid.CreateVersion4();
    static readonly Uuid AdaMemberId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);
    static readonly ActorReference Owner = ActorReference.ForMember(OwnerMemberId, "Owner");

    [Fact]
    public void ShouldAssignPendingAcknowledgementToThePersonsMemberGivenALinkedMember()
    {
        // Arrange
        var campaign = Launch(Ada, Bob);

        // Act
        var work = PolicyCampaignWork.Candidates(campaign, Today,
            new Dictionary<Uuid, Uuid> { [Ada] = AdaMemberId });

        // Assert
        var ada = Assert.Single(work, item => item.Summary.StartsWith("Acknowledge", StringComparison.Ordinal));
        Assert.Equal(new OperatingHolder(OperatingAuthority.MemberHolder, AdaMemberId), ada.Responsible);
        Assert.Equal(PolicyCampaignWork.Acknowledgement, ada.Kind);
        Assert.Equal("acknowledge", ada.NextAction);
        Assert.Equal(Today.AddDays(30), ada.DueOn);
        Assert.EndsWith($"/campaigns/{campaign.Id}/acknowledgements", ada.ActionPath, StringComparison.Ordinal);
        Assert.Contains("POL-AC", ada.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldAssignNonMemberAcknowledgementToTheCampaignOwnerGivenNoLinkedMember()
    {
        // Arrange
        var campaign = Launch(Ada, Bob);

        // Act
        var work = PolicyCampaignWork.Candidates(campaign, Today,
            new Dictionary<Uuid, Uuid> { [Ada] = AdaMemberId });

        // Assert
        var bob = Assert.Single(work, item => item.Summary.StartsWith("Collect", StringComparison.Ordinal));
        Assert.Equal(new OperatingHolder(OperatingAuthority.MemberHolder, OwnerMemberId), bob.Responsible);
        Assert.Equal("record_acknowledgement", bob.NextAction);
        Assert.Contains(campaign.DisplayNameOf(Bob)!, bob.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldGiveEachPersonADistinctStableWorkItemGivenOneCampaign()
    {
        // Arrange
        var campaign = Launch(Ada, Bob);

        // Act
        var first = PolicyCampaignWork.Candidates(campaign, Today, new Dictionary<Uuid, Uuid>());
        var again = PolicyCampaignWork.Candidates(campaign, Today, new Dictionary<Uuid, Uuid>());

        // Assert
        Assert.Equal(2, first.Select(static item => item.WorkItemId).Distinct().Count());
        Assert.Equal(first.Select(static item => item.WorkItemId), again.Select(static item => item.WorkItemId));
    }

    [Fact]
    public void ShouldDropSatisfiedAndExceptedPeopleGivenAcknowledgementAndWaiver()
    {
        // Arrange
        var campaign = Launch(Ada, Bob);
        Assert.Null(campaign.Acknowledge(ProgramId, Uuid.CreateVersion4(), Ada, 2, "sha-v2",
            PolicyDistributionCampaign.DefaultAcknowledgementText,
            new ActorReference("workforce_person", Ada.ToString(), "Ada"), Owner, true, Now.AddDays(1)));
        Assert.Null(campaign.ApproveWaiver(ProgramId, Uuid.CreateVersion4(), Bob, "On leave",
            Today.AddMonths(3), Owner, OwnerMemberId, Now.AddDays(1)));

        // Act
        var work = PolicyCampaignWork.Candidates(campaign, Today.AddDays(2), new Dictionary<Uuid, Uuid>());

        // Assert
        Assert.Empty(work);
    }

    [Fact]
    public void ShouldYieldNoWorkGivenAClosedCampaign()
    {
        // Arrange
        var campaign = Launch(Ada);
        Assert.Null(campaign.Close(ProgramId, "Period ended", Owner, Now.AddDays(40)));

        // Act
        var work = PolicyCampaignWork.Candidates(campaign, Today.AddDays(41), new Dictionary<Uuid, Uuid>());

        // Assert
        Assert.Empty(work);
    }

    [Fact]
    public void ShouldAskForCompletionGivenATrainingCampaign()
    {
        // Arrange
        var campaign = Launch([Ada], new CampaignSubject("training", Uuid.CreateVersion4(), "TR-SEC",
            "Security awareness", 1, "sha-t1"));

        // Act
        var work = PolicyCampaignWork.Candidates(campaign, Today,
            new Dictionary<Uuid, Uuid> { [Ada] = AdaMemberId });

        // Assert
        var item = Assert.Single(work);
        Assert.Equal(PolicyCampaignWork.TrainingCompletion, item.Kind);
        Assert.EndsWith($"/campaigns/{campaign.Id}/completions", item.ActionPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldLoadOpenCampaignWorkWithLinkedMembersGivenProgramCampaigns()
    {
        // Arrange
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton(TimeProvider.System);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var dispatch = new RequestDispatchContext(RequestActor.System);
        var userId = Uuid.CreateVersion4();
        var person = new Person(TenantId, Ada);
        Assert.True(person.Record("Ada", null, Owner, Now).IsSuccess);
        Assert.Null(person.CorrelateMembership(1, userId, Owner, Now));
        await writer.SaveAsync(person, dispatch, CancellationToken.None);
        var open = Launch(Ada, Bob);
        await writer.SaveAsync(open, dispatch, CancellationToken.None);
        var closed = Launch(Ada);
        Assert.Null(closed.Close(ProgramId, "Done", Owner, Now.AddDays(1)));
        await writer.SaveAsync(closed, dispatch, CancellationToken.None);
        var checkpoint = await CheckpointAfterAsync(events);
        var directory = new Campaigns(checkpoint, Summary(open, "open"), Summary(closed, "closed"));

        // Act
        var work = await PolicyCampaignWork.LoadAsync(reader, directory,
            new CampaignDirectoryReadConsistency(directory, events), TenantId, ProgramId, Today,
            CancellationToken.None);

        // Assert
        Assert.True(work.IsSuccess);
        Assert.Equal(2, work.Value.Count);
        Assert.Contains(work.Value, item => item.Responsible ==
            new OperatingHolder(OperatingAuthority.MemberHolder, RbacIds.Member(TenantId, userId)));
        Assert.Contains(work.Value, item => item.Responsible ==
            new OperatingHolder(OperatingAuthority.MemberHolder, OwnerMemberId));
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenCampaignDirectoryBehindSource()
    {
        // Arrange
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton(TimeProvider.System);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var campaign = Launch(Ada);
        await writer.SaveAsync(campaign, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var directory = new Campaigns(ProjectionCheckpoint.Start);

        // Act
        var result = await PolicyCampaignWork.LoadAsync(reader, directory,
            new CampaignDirectoryReadConsistency(directory, events), TenantId, ProgramId, Today,
            CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenCampaignSourceChangesDuringRead()
    {
        // Arrange
        var services = new ServiceCollection();
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton(TimeProvider.System);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var dispatch = new RequestDispatchContext(RequestActor.System);
        var existing = Launch(Ada);
        await writer.SaveAsync(existing, dispatch, CancellationToken.None);
        var checkpoint = await CheckpointAfterAsync(events);
        var directory = new Campaigns(checkpoint, Summary(existing, "open"))
        {
            OnList = async _ =>
            {
                await writer.SaveAsync(Launch(Bob), dispatch, CancellationToken.None);
            },
        };

        // Act
        var result = await PolicyCampaignWork.LoadAsync(reader, directory,
            new CampaignDirectoryReadConsistency(directory, events), TenantId, ProgramId, Today,
            CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    static CampaignSummaryView Summary(PolicyDistributionCampaign campaign, string status) =>
        new(TenantId, ProgramId, campaign.Id, "policy", Uuid.CreateVersion4(), "POL-AC", 2,
            "Access Control Policy", Today.AddDays(30), status, 1, Now);

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore events)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           TenantId.ToString(), "policy-distribution-campaigns"), cursor,
                           CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed class Campaigns(ProjectionCheckpoint checkpoint, params CampaignSummaryView[] items)
        : ICampaignDirectoryReader
    {
        public ProjectionCheckpoint Checkpoint { get; set; } = checkpoint;
        public Func<CancellationToken, ValueTask>? OnList { get; set; }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(Checkpoint);

        public async ValueTask<Page<CampaignSummaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default)
        {
            if (OnList is not null)
                await OnList(ct);
            return new Page<CampaignSummaryView>(items, null);
        }

        public ValueTask<IReadOnlyList<Uuid>> ListForSubjectAsync(Uuid tenantId, Uuid programId,
            Uuid subjectId, long version, CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<Uuid>>([]);
    }

    static PolicyDistributionCampaign Launch(params Uuid[] people) =>
        Launch(people, new CampaignSubject("policy", Uuid.CreateVersion4(), "POL-AC",
            "Access Control Policy", 2, "sha-v2"));

    static PolicyDistributionCampaign Launch(Uuid[] people, CampaignSubject subject)
    {
        var jobs = people.Select(static person => new FrozenWorkRelationship(Uuid.CreateVersion4(), 1,
            person, person.ToString()[..8], "employee", "active", new DateOnly(2025, 1, 1), null,
            null, null, null)).ToArray();
        var roster = new WorkforceRosterSnapshotView(TenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            null, "sha", jobs.Length, null, Owner, Now,
            people.Select(static person => new FrozenPerson(person, 1, "Person " + person.ToString()[..4], null))
                .ToArray(), jobs, true);
        var campaign = new PolicyDistributionCampaign(TenantId, Uuid.CreateVersion4());
        Assert.True(campaign.Launch(ProgramId, subject, PolicyAudience.CoreSecurity, [], roster.SnapshotId,
            roster.ContentSha256, RosterAudience.Evaluate(roster, PolicyAudience.CoreSecurity, []),
            Today.AddDays(30), "Read and acknowledge", Owner, OwnerMemberId, Now).IsSuccess);
        return campaign;
    }
}
