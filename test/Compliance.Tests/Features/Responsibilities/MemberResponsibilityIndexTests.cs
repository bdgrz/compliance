using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Responsibilities;

public sealed class MemberResponsibilityIndexTests
{
    [Fact]
    public async Task ShouldListOpenAssignmentsAndPreserveRevocationGivenProjectionReplay()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var index = new FitzMemberResponsibilityIndex(client);
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var scope = new ResponsibilityScope("boundary", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 1);
        var assigned = new ResponsibilityAssigned(tenantId, Uuid.CreateVersion4(), memberId,
            ResponsibilityType.ControlOwner, scope, now, Uuid.CreateVersion4(), now, null,
            [], "Administrator");
        var revoked = new ResponsibilityRevoked(tenantId, assigned.AssignmentId, scope,
            Uuid.CreateVersion4(), now.AddMinutes(1), "Reassigned", "Administrator");
        var identity = new CheckpointIdentity("MemberResponsibilitiesV1",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await index.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await index.ApplyAsync(assigned);
            await index.ApplyAsync(assigned);
            await index.ApplyAsync(revoked);
            await index.ApplyAsync(revoked);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        var assignments = await index.GetAsync(tenantId, memberId);
        var assignment = Assert.Single(assignments);
        Assert.Equal(assigned.AssignmentId, assignment.AssignmentId);
        Assert.Equal(revoked.RevokedAt, assignment.RevokedAt);
        Assert.Equal(revoked.Reason, assignment.RevocationReason);
        Assert.Empty(await index.GetAsync(Uuid.CreateVersion4(), memberId));
    }
}
