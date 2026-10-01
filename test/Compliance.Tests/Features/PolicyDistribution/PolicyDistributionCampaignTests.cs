using System.Text.Json;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution;

public sealed class PolicyDistributionCampaignTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ManagerId = Uuid.CreateVersion4();
    static readonly Uuid Ada = Uuid.CreateVersion4();
    static readonly Uuid Bob = Uuid.CreateVersion4();
    static readonly Uuid Cy = Uuid.CreateVersion4();
    static readonly Uuid Dee = Uuid.CreateVersion4();
    static readonly Uuid Eve = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);
    static readonly DateOnly Due = Today.AddDays(30);
    static readonly CampaignSubject PolicySubject = new("policy", Uuid.CreateVersion4(),
        "POL-AC", "Access Control Policy", 2, "sha-v2");
    static readonly ActorReference Manager = ActorReference.ForMember(ManagerId, "Manager");

    static FrozenWorkRelationship Job(Uuid personId, string workerType, string status,
        string? department, DateOnly? start = null) => new(Uuid.CreateVersion4(), 1, personId,
        personId.ToString()[..8], workerType, status, start ?? new DateOnly(2025, 1, 1), null,
        department, null, null);

    static WorkforceRosterSnapshotView Roster(params FrozenWorkRelationship[] jobs) => new(
        TenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), null, "sha", jobs.Length, null,
        Manager, Now, jobs.Select(static job => new FrozenPerson(job.PersonId, 1,
            "Person " + job.PersonId.ToString()[..4], null)).DistinctBy(static p => p.PersonId)
            .ToArray(), jobs, true);

    static PolicyDistributionCampaign Launch(WorkforceRosterSnapshotView roster,
        string kind = PolicyAudience.CoreSecurity, IReadOnlyList<string>? teams = null,
        CampaignSubject? subject = null)
    {
        teams ??= [];
        var campaign = new PolicyDistributionCampaign(TenantId, Uuid.CreateVersion4());
        Assert.True(campaign.Launch(ProgramId, subject ?? PolicySubject, kind, teams,
            roster.SnapshotId, roster.ContentSha256, RosterAudience.Evaluate(roster, kind, teams),
            Due, "Read and acknowledge", Manager, ManagerId, Now).IsSuccess);
        return campaign;
    }

    static CommandFailure? Acknowledge(PolicyDistributionCampaign campaign, Uuid person,
        long version = 2, string hash = "sha-v2", string? text = null, DateTimeOffset? at = null) =>
        campaign.Acknowledge(ProgramId, Uuid.CreateVersion4(), person, version, hash,
            text ?? PolicyDistributionCampaign.DefaultAcknowledgementText,
            new ActorReference("workforce_person", person.ToString(), "Person"), Manager, true,
            at ?? Now.AddDays(1));

    [Fact]
    public void ShouldFreezeEmployeesAndContractorsGivenCoreSecurityRule()
    {
        // Arrange
        var roster = Roster(Job(Ada, "employee", "active", "Engineering"),
            Job(Bob, "contractor", "on_leave", "Sales"),
            Job(Cy, "external_collaborator", "active", "Engineering"),
            Job(Dee, "employee", "ended", "Engineering"));

        // Act
        var campaign = Launch(roster);

        // Assert
        var people = campaign.Participants(Today, null);
        Assert.Equal(new[] { Ada, Bob }.Order(), people.Select(static p => p.PersonId).Order());
        Assert.All(people, person => Assert.Equal("launch", person.Inclusion));
        Assert.All(people, person => Assert.Equal(roster.SnapshotId, person.IncludedBySnapshotId));
        Assert.Equal(new CampaignTotalsView(2, 2, 0, 0, 2, 0, 0, 0), campaign.Totals(Today));
    }

    [Fact]
    public void ShouldSelectNamedRosterTeamsGivenRoleTargetedRule()
    {
        // Arrange
        var roster = Roster(Job(Ada, "employee", "active", " engineering "),
            Job(Bob, "contractor", "active", "Sales"),
            Job(Cy, "external_collaborator", "active", "Engineering"));

        // Act
        var campaign = Launch(roster, PolicyAudience.RoleTargeted, ["Engineering"]);

        // Assert
        Assert.Equal(new[] { Ada, Cy }.Order(),
            campaign.Participants(Today, null).Select(static p => p.PersonId).Order());
    }

    [Fact]
    public void ShouldAmendWithoutRewritingLaunchAudienceGivenLaterRoster()
    {
        // Arrange
        var launch = Roster(Job(Ada, "employee", "active", "Engineering"),
            Job(Bob, "employee", "active", "Engineering"),
            Job(Cy, "employee", "active", "Sales"),
            Job(Dee, "employee", "active", "Engineering"));
        var campaign = Launch(launch, PolicyAudience.RoleTargeted, ["Engineering"]);
        Assert.Null(Acknowledge(campaign, Bob));
        var later = Roster(Job(Ada, "employee", "active", "Engineering"),
            Job(Bob, "employee", "active", "Sales"),
            Job(Cy, "employee", "active", "Engineering"),
            Job(Eve, "contractor", "pending", "Engineering", Today.AddDays(5)));
        var previous = RosterAudience.Evaluate(launch, PolicyAudience.RoleTargeted,
            ["Engineering"]).Present;

        // Act
        var result = campaign.Reconcile(ProgramId, later.SnapshotId, later.ContentSha256,
            RosterAudience.Evaluate(later, PolicyAudience.RoleTargeted, ["Engineering"]),
            previous, Manager, Now.AddDays(2));

        // Assert
        Assert.True(result.IsSuccess);
        var reasons = result.Value.Amendments.ToDictionary(static a => a.PersonId,
            static a => a.Reason);
        Assert.Equal("mover_out", reasons[Bob]);
        Assert.Equal("leaver", reasons[Dee]);
        Assert.Equal("mover_in", reasons[Cy]);
        Assert.Equal("joiner", reasons[Eve]);
        Assert.Equal(Today.AddDays(35), result.Value.Amendments.Single(a => a.PersonId == Eve).DueOn);
        var people = campaign.Participants(Today, null).ToDictionary(static p => p.PersonId);
        Assert.Equal("removed", people[Bob].State);
        Assert.NotNull(people[Bob].Acknowledgement);
        Assert.Equal("launch", people[Ada].Inclusion);
        Assert.Equal(later.SnapshotId, people[Eve].IncludedBySnapshotId);
        var totals = campaign.Totals(Today);
        Assert.Equal(new CampaignTotalsView(5, 3, 2, 2, 3, 0, 0, 0), totals);
        Assert.Equal(totals.Population, totals.Pending + totals.Overdue + totals.Satisfied +
                                        totals.Excepted + totals.Removed);
        Assert.Equal(4, campaign.Amendments().Count);
        Assert.False(campaign.Reconcile(ProgramId, later.SnapshotId, later.ContentSha256,
            RosterAudience.Evaluate(later, PolicyAudience.RoleTargeted, ["Engineering"]),
            previous, Manager, Now.AddDays(3)).IsSuccess);
    }

    [Fact]
    public void ShouldRejectAcknowledgementGivenWrongVersionHashOrText()
    {
        // Arrange
        var campaign = Launch(Roster(Job(Ada, "employee", "active", null)));

        // Act
        var laterVersion = Acknowledge(campaign, Ada, version: 3);
        var wrongHash = Acknowledge(campaign, Ada, hash: "sha-v3");
        var wrongText = Acknowledge(campaign, Ada, text: "I agree.");
        var outsider = Acknowledge(campaign, Bob);
        var exact = Acknowledge(campaign, Ada);
        var duplicate = Acknowledge(campaign, Ada);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(laterVersion).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(wrongHash).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(wrongText).Code);
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(outsider).Code);
        Assert.Null(exact);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(duplicate).Code);
        var acknowledgement = campaign.FindAcknowledgement(Ada)!;
        Assert.Equal("sha-v2", acknowledgement.ContentSha256);
        Assert.Equal(PolicyDistributionCampaign.DefaultAcknowledgementText,
            acknowledgement.AcknowledgementText);
        Assert.True(acknowledgement.RecordedOnBehalf);
        Assert.Equal("workforce_person", acknowledgement.Performer.Kind);
        Assert.Equal(Manager, acknowledgement.Recorder);
        Assert.Equal("acknowledged", campaign.StateOf(Ada, Today.AddDays(1)));
        Assert.Equal("pending", campaign.StateOf(Ada, Today));
    }

    [Fact]
    public void ShouldCountExceptedUntilWaiverExpiresGivenApprovedWaiver()
    {
        // Arrange
        var campaign = Launch(Roster(Job(Ada, "employee", "active", null)));
        var expires = Today.AddMonths(2);

        // Act
        var tooLong = campaign.ApproveWaiver(ProgramId, Uuid.CreateVersion4(), Ada, "Leave",
            Today.AddMonths(12).AddDays(1), Manager, ManagerId, Now);
        var approved = campaign.ApproveWaiver(ProgramId, Uuid.CreateVersion4(), Ada,
            "Extended leave", expires, Manager, ManagerId, Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(tooLong).Code);
        Assert.Null(approved);
        Assert.Equal("excepted", campaign.StateOf(Ada, expires.AddDays(-1)));
        Assert.Equal("overdue", campaign.StateOf(Ada, expires));
        Assert.Equal(1, campaign.Totals(Today).Excepted);
        Assert.Equal(0, campaign.Totals(Today).Satisfied);
        Assert.Equal("pending", campaign.StateOf(Ada, Today.AddDays(-1)));
    }

    [Fact]
    public void ShouldRecordExactCompletionAndRejectDuplicateGivenTrainingCampaign()
    {
        // Arrange
        var subject = new CampaignSubject("training", Uuid.CreateVersion4(), "SAT", "Security awareness",
            1, "sha-course");
        var campaign = Launch(Roster(Job(Ada, "employee", "active", null),
            Job(Bob, "employee", "active", null)), subject: subject);

        // Act
        var wrongVersion = campaign.RecordCompletion(ProgramId, Uuid.CreateVersion4(), Ada, 2,
            Today, "lms_export", "export-2026-10.csv#12", Manager, Now);
        var badSource = campaign.RecordCompletion(ProgramId, Uuid.CreateVersion4(), Ada, 1,
            Today, "connector", "row", Manager, Now);
        var future = campaign.RecordCompletion(ProgramId, Uuid.CreateVersion4(), Ada, 1,
            Today.AddDays(1), "manual", "certificate.pdf", Manager, Now);
        var recorded = campaign.RecordCompletion(ProgramId, Uuid.CreateVersion4(), Ada, 1, Today,
            "lms_export", "export-2026-10.csv#12", Manager, Now);
        var duplicate = campaign.RecordCompletion(ProgramId, Uuid.CreateVersion4(), Ada, 1, Today,
            "manual", "certificate.pdf", Manager, Now);
        var acknowledgement = Acknowledge(campaign, Bob, version: 1, hash: "sha-course");

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(wrongVersion).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(badSource).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(future).Code);
        Assert.Null(recorded);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(duplicate).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(acknowledgement).Code);
        Assert.Equal("completed", campaign.StateOf(Ada, Today));
        Assert.Equal("overdue", campaign.StateOf(Bob, Due.AddDays(1)));
    }

    [Fact]
    public void ShouldFreezeClosingTotalsAndRejectLaterResultsGivenClosedCampaign()
    {
        // Arrange
        var campaign = Launch(Roster(Job(Ada, "employee", "active", null),
            Job(Bob, "employee", "active", null)));
        Assert.Null(Acknowledge(campaign, Ada));

        // Act
        var closed = campaign.Close(ProgramId, "Annual cycle complete", Manager, Now.AddDays(40));
        var late = Acknowledge(campaign, Bob, at: Now.AddDays(41));

        // Assert
        Assert.Null(closed);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(late).Code);
        var view = campaign.ToView(Today.AddDays(40));
        Assert.Equal("closed", view.Status);
        Assert.Equal(new CampaignTotalsView(2, 2, 0, 0, 0, 1, 1, 0), view.ClosingTotals);
    }

    [Fact]
    public void ShouldBatchLargeAudienceAndReplayGivenRoundTrippedEvents()
    {
        // Arrange
        var jobs = Enumerable.Range(0, PolicyDistributionCampaign.BatchSize * 2 + 3)
            .Select(_ => Job(Uuid.CreateVersion4(), "employee", "active", null)).ToArray();
        var source = Launch(Roster(jobs));
        Assert.Null(Acknowledge(source, jobs[0].PersonId));
        var events = new AggregateScenario<PolicyDistributionCampaign>(source).PendingEvents;
        var seeded = events.Select((ev, index) =>
            DomainEventSeed.Attach(RoundTrip(ev), source.Id, (ulong)index + 1)).ToArray();

        // Act
        var replayed = new AggregateScenario<PolicyDistributionCampaign>(
            new PolicyDistributionCampaign(TenantId, source.Id)).Given(seeded).Aggregate;

        // Assert
        Assert.Equal(3, events.OfType<PolicyCampaignAudienceFrozen>().Count());
        Assert.All(events, ev => Assert.True(JsonSerializer.SerializeToUtf8Bytes(ev, ev.GetType(),
            ComplianceCoreJsonContext.Default).Length < 48 * 1024));
        Assert.Equal(source.Totals(Today.AddDays(2)), replayed.Totals(Today.AddDays(2)));
        Assert.Equal(jobs.Length, replayed.Totals(Today).Population);
    }

    static DomainEvent RoundTrip(DomainEvent ev) => (DomainEvent)JsonSerializer.Deserialize(
        JsonSerializer.Serialize(ev, ev.GetType(), ComplianceCoreJsonContext.Default),
        ev.GetType(), ComplianceCoreJsonContext.Default)!;
}
