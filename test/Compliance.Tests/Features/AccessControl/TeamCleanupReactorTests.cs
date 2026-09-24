using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TeamCleanupReactorTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid TeamId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRemoveMembersGivenDeletedTeam()
    {
        // Arrange
        var firstMember = Uuid.CreateVersion4();
        var secondMember = Uuid.CreateVersion4();
        var members = new FakeTeamMemberDirectoryReader(
            new TeamMemberView(TeamId, firstMember), new TeamMemberView(TeamId, secondMember));
        var scenario = new ReactorScenario().Given(new TeamDeleted(TenantId, TeamId));

        // Act
        await scenario.RunAsync(new TeamCleanupReactor(new InMemoryProjectionCheckpointStore(),
            scenario.Requests, members));

        // Assert
        Assert.Equal(2, scenario.SentRequests.Count);
        Assert.Contains(scenario.SentRequests, request =>
            request is RemoveTeamMember removal && removal.MemberId == firstMember);
        Assert.Contains(scenario.SentRequests, request =>
            request is RemoveTeamMember removal && removal.MemberId == secondMember);
        Assert.All(scenario.SentRequests, request =>
        {
            var removal = Assert.IsType<RemoveTeamMember>(request);
            Assert.Equal(TenantId, removal.TenantId);
            Assert.Equal(TeamId, removal.TeamId);
        });
    }

    [Fact]
    public async Task ShouldDispatchNothingGivenAnAlreadyEmptyTeam()
    {
        // Arrange
        var members = new FakeTeamMemberDirectoryReader();
        var scenario = new ReactorScenario().Given(new TeamDeleted(TenantId, TeamId));

        // Act
        await scenario.RunAsync(new TeamCleanupReactor(new InMemoryProjectionCheckpointStore(),
            scenario.Requests, members));

        // Assert
        Assert.Empty(scenario.SentRequests);
    }

    sealed class FakeTeamMemberDirectoryReader(params TeamMemberView[] members) : ITeamMemberDirectoryReader
    {
        public ValueTask<Page<TeamMemberView>> ListAsync(
            Uuid tenantId,
            Uuid teamId,
            int? limit,
            string? cursor,
            string? search,
            bool descending,
            CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TeamMemberView>(members, null));
    }
}
