using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Policies;

public sealed class FitzPolicyDirectoryTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    static PolicyContent Content(string title, int cadence = 12) => new(title, "Purpose",
        PolicyAudience.CoreSecurity, null, cadence, "Body", null, "Owner", []);

    static ActorReference Actor(Uuid memberId) => ActorReference.ForMember(memberId, "Member");

    [Fact]
    public async Task ShouldProjectLifecycleAndDeleteDiscardedDraftGivenPolicyEvents()
    {
        // Arrange
        var directory = new FitzPolicyDirectory(new InMemoryKvClient());
        var policy = new Policy(TenantId, Policy.IdFor(TenantId, ProgramId, "POL-B"));
        Assert.True(policy.Create(ProgramId, Uuid.CreateVersion4(), "POL-B", Content("Draft"),
            Actor(AuthorId), AuthorId, Now).IsSuccess);
        Assert.Null(policy.Revise(ProgramId, 1, Content("Reviewed", 6), Actor(AuthorId), AuthorId,
            Now));
        var review = Uuid.CreateVersion4();
        Assert.Null(policy.Review(ProgramId, 2, review, "accept", "Ok", Actor(ReviewerId),
            ReviewerId, Now));
        Assert.Null(policy.Approve(ProgramId, 2, Uuid.CreateVersion4(), review,
            new DateOnly(2026, 10, 1), true, "Ok", null, Actor(ApproverId), ApproverId, Now));
        Assert.Null(policy.ProposeSuccessor(ProgramId, 1, Content("Successor"), Actor(AuthorId),
            AuthorId, Now));
        var unused = new Policy(TenantId, Policy.IdFor(TenantId, ProgramId, "POL-A"));
        Assert.True(unused.Create(ProgramId, Uuid.CreateVersion4(), "POL-A", Content("Unused"),
            Actor(AuthorId), AuthorId, Now).IsSuccess);
        Assert.Null(unused.Discard(ProgramId, 1, "Not needed", Actor(AuthorId), Now));
        var other = new Policy(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(TenantId), ProjectionCheckpoint.Start)))
        {
            foreach (var ev in new AggregateScenario<Policy>(policy).PendingEvents
                         .Concat(new AggregateScenario<Policy>(unused).PendingEvents))
                await directory.ApplyAsync(ev);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(new PolicyDraftDiscarded(TenantId, ProgramId,
                    other.Id, 1, "Out of order", Actor(AuthorId), Now)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var page = await directory.ListProgramAsync(TenantId, ProgramId, 50, null);
        var alien = await directory.ListProgramAsync(Uuid.CreateVersion4(), ProgramId, 50, null);

        // Assert
        var row = Assert.Single(page.Items);
        Assert.Equal("POL-B", row.Identifier);
        Assert.Equal("Reviewed", row.Title);
        Assert.Equal("approved", row.Status);
        Assert.Equal("draft", row.PendingStatus);
        Assert.Equal(1, row.CurrentVersion);
        Assert.Equal(3, row.Revision);
        Assert.Equal(new DateOnly(2027, 3, 30), row.NextReviewDueOn);
        Assert.Empty(alien.Items);
    }

    [Fact]
    public async Task ShouldListCampaignsBoundToExactVersionGivenLaunchedAndClosedCampaigns()
    {
        // Arrange
        var directory = new FitzCampaignDirectory(new InMemoryKvClient());
        var policyId = Uuid.CreateVersion4();
        var first = Launched(policyId, 1, Now);
        var second = Launched(policyId, 2, Now.AddDays(1));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity(FitzCampaignDirectory.ProjectorName,
                             EventStreamPattern.ForPattern(TenantId.ToString(),
                                 "policy-distribution-campaigns")), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first);
            await directory.ApplyAsync(second);
            await directory.ApplyAsync(new PolicyCampaignClosed(TenantId, first.CampaignId,
                new DateOnly(2026, 11, 1), "Done", new CampaignTotalsView(0, 0, 0, 0, 0, 0, 0, 0),
                Actor(AuthorId), Now.AddDays(30)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var page = await directory.ListProgramAsync(TenantId, ProgramId, 50, null);
        var boundToFirst = await directory.ListForSubjectAsync(TenantId, ProgramId, policyId, 1);

        // Assert
        Assert.Equal([first.CampaignId, second.CampaignId],
            page.Items.Select(static campaign => campaign.CampaignId));
        Assert.Equal("closed", page.Items[0].Status);
        Assert.Equal("open", page.Items[1].Status);
        Assert.Equal([first.CampaignId], boundToFirst);
    }

    static PolicyCampaignLaunched Launched(Uuid policyId, long version, DateTimeOffset at) =>
        new(TenantId, ProgramId, Uuid.CreateVersion4(),
            new CampaignSubject("policy", policyId, "POL-B", "Policy", version, "sha"),
            PolicyAudience.CoreSecurity, [], Uuid.CreateVersion4(), "roster",
            DateOnly.FromDateTime(at.UtcDateTime), DateOnly.FromDateTime(at.UtcDateTime).AddDays(30),
            null, PolicyDistributionCampaign.DefaultAcknowledgementText, 3, Actor(AuthorId),
            AuthorId, at);

    static CheckpointIdentity Identity(Uuid tenantId) => new(FitzPolicyDirectory.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), "policies"));
}
