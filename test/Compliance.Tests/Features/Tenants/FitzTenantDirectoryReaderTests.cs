using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class FitzTenantDirectoryReaderTests
{
    [Fact]
    public async Task ShouldReturnStoredTenantGivenMatchingId()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var tenantId = Uuid.CreateVersion4();
        await SeedAsync(client, tenantId, "Acme", "acme");
        var reader = new FitzTenantDirectoryReader(client);

        // Act
        var tenant = await reader.GetAsync(tenantId, CancellationToken.None);

        // Assert
        Assert.NotNull(tenant);
        Assert.Equal("Acme", tenant.Name);
        Assert.Equal("acme", tenant.Slug);
    }

    [Fact]
    public async Task ShouldReturnNullGivenNoSuchTenant()
    {
        // Arrange
        var reader = new FitzTenantDirectoryReader(new InMemoryKvClient());

        // Act
        var tenant = await reader.GetAsync(Uuid.CreateVersion4(), CancellationToken.None);

        // Assert
        Assert.Null(tenant);
    }

    [Fact]
    public async Task ShouldProjectActiveTenantWithoutOperatorOrInvitationGivenVerifiedCreator()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        var repository = new FitzTenantDirectoryReader(client);
        var identity = new CheckpointIdentity("TenantDirectory", EventStreamPattern.ForPattern("bdgrz", "tenants"));

        // Act
        await using (var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await repository.ApplyAsync(new TenantRegistered(tenantId, creatorId, "Acme", "acme",
                "Acme LLC", "creator@example.com", CreatorIsAdministrator: true));
            await repository.ApplyAsync(new TenantSlugConfirmed(tenantId, "acme"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        var tenant = await repository.GetAsync(tenantId);
        Assert.NotNull(tenant);
        Assert.Equal("active", tenant.Status);
        Assert.Null(tenant.OperatorUserId);
        Assert.False(tenant.RequiresInvitation);
    }

    [Fact]
    public async Task ShouldProjectLifecycleGivenSuspensionAndReactivation()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var tenantId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        var repository = new FitzTenantDirectoryReader(client);
        var identity = new CheckpointIdentity("TenantDirectory", EventStreamPattern.ForPattern("bdgrz", "tenants"));

        // Act
        await using (var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await repository.ApplyAsync(new TenantRegistered(tenantId, operatorId, "Acme", "acme"));
            await repository.ApplyAsync(new TenantSlugConfirmed(tenantId, "acme"));
            await repository.ApplyAsync(new TenantSuspended(tenantId, operatorId));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Equal("suspended", (await repository.GetAsync(tenantId))?.Status);

        await using (var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await repository.ApplyAsync(new TenantReactivated(tenantId, operatorId));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        Assert.Equal("active", (await repository.GetAsync(tenantId))?.Status);
    }

    [Fact]
    public async Task ShouldPageAllTenantLifecycleStatesGivenPrimaryDirectory()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var repository = new FitzTenantDirectoryReader(client);
        var identity = new CheckpointIdentity("TenantDirectory", EventStreamPattern.ForPattern("bdgrz", "tenants"));
        var operatorId = Uuid.CreateVersion4();
        var provisioningId = Uuid.CreateVersion4();
        var activeId = Uuid.CreateVersion4();
        var suspendedId = Uuid.CreateVersion4();
        var rejectedId = Uuid.CreateVersion4();
        await using (var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await repository.ApplyAsync(new TenantRegistered(provisioningId, operatorId,
                "Provisioning", "provisioning", FirstAdministratorEmail: "admin@example.com"));
            await repository.ApplyAsync(new TenantRegistered(activeId, operatorId, "Active", "active"));
            await repository.ApplyAsync(new TenantSlugConfirmed(activeId, "active"));
            await repository.ApplyAsync(new TenantRegistered(suspendedId, operatorId, "Suspended", "suspended"));
            await repository.ApplyAsync(new TenantSlugConfirmed(suspendedId, "suspended"));
            await repository.ApplyAsync(new TenantSuspended(suspendedId, operatorId));
            await repository.ApplyAsync(new TenantRegistered(rejectedId, operatorId, "Rejected", "rejected"));
            await repository.ApplyAsync(new TenantSlugRejected(rejectedId, "rejected"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var seen = new List<TenantView>();
        string? cursor = null;
        do
        {
            var page = await repository.ListAsync(1, cursor);
            seen.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);

        // Assert
        Assert.Equal(4, seen.Count);
        Assert.Equal(new[] { activeId, provisioningId, rejectedId, suspendedId }
                .OrderBy(id => id.ToString(), StringComparer.Ordinal),
            seen.Select(tenant => tenant.TenantId));
        Assert.Contains(seen, tenant => tenant.TenantId == provisioningId && tenant.Status == "provisioning");
        Assert.Contains(seen, tenant => tenant.TenantId == activeId && tenant.Status == "active");
        Assert.Contains(seen, tenant => tenant.TenantId == suspendedId && tenant.Status == "suspended");
        Assert.Contains(seen, tenant => tenant.TenantId == rejectedId && tenant.Status == "rejected");
    }

    static async Task SeedAsync(InMemoryKvClient client, Uuid tenantId, string name, string slug)
    {
        var repository = new FitzTenantDirectoryReader(client);
        var identity = new CheckpointIdentity("TenantDirectory", EventStreamPattern.ForPattern("bdgrz", "tenants"));
        await using var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await repository.ApplyAsync(new TenantRegistered(tenantId, Uuid.CreateVersion4(), name, slug));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
