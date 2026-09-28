using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Responsibilities;

public sealed class ResponsibilitySetProjectionTests
{
    [Fact]
    public async Task ShouldKeepOneRevisionGivenDuplicateAssignmentAndRevocationEvents()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var directory = new FitzResponsibilitySetDirectory(client);
        var tenantId = Uuid.CreateVersion4();
        var scope = new ResponsibilityScope("boundary", Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 3);
        var assignedAt = DateTimeOffset.UtcNow;
        var assigned = new ResponsibilityAssigned(tenantId, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), ResponsibilityType.ControlOwner, scope, assignedAt,
            Uuid.CreateVersion4(), assignedAt, null, [], "Admin");
        var revoked = new ResponsibilityRevoked(tenantId, assigned.AssignmentId, scope,
            Uuid.CreateVersion4(), assignedAt.AddMinutes(1), "Work complete", "Admin");
        var identity = new CheckpointIdentity("ResponsibilitySetsV1",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(assigned);
            await directory.ApplyAsync(assigned);
            await directory.ApplyAsync(revoked);
            await directory.ApplyAsync(revoked);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var view = await directory.GetAsync(tenantId, scope);

        // Assert
        Assert.NotNull(view);
        Assert.Equal(2, view.Revision);
        var assignment = Assert.Single(view.Assignments);
        Assert.Equal(revoked.RevokedAt, assignment.RevokedAt);
        Assert.Equal(revoked.Reason, assignment.RevocationReason);
    }

    [Fact]
    public async Task ShouldSeparateTenantAndSourceRevisionGivenSameRecordIdentity()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var directory = new FitzResponsibilitySetDirectory(client);
        var tenantA = Uuid.CreateVersion4();
        var tenantB = Uuid.CreateVersion4();
        var recordId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var scope = new ResponsibilityScope("boundary", recordId, versionId, 1);
        var assignedAt = DateTimeOffset.UtcNow;
        var ev = new ResponsibilityAssigned(tenantA, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), ResponsibilityType.ControlOwner, scope, assignedAt,
            Uuid.CreateVersion4(), assignedAt, null, [], "Admin");
        var identity = new CheckpointIdentity("ResponsibilitySetsV1",
            EventStreamPattern.ForPattern(tenantA.ToString()));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(ev);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var sameTenant = await directory.GetAsync(tenantA, scope);
        var otherTenant = await directory.GetAsync(tenantB, scope);
        var nextRevision = await directory.GetAsync(tenantA, scope with { Revision = 2 });

        // Assert
        Assert.NotNull(sameTenant);
        Assert.Equal(tenantA, sameTenant.TenantId);
        Assert.Null(otherTenant);
        Assert.Null(nextRevision);
    }
}
