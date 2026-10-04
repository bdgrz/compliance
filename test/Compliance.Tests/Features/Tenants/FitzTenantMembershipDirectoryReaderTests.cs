using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class FitzTenantMembershipDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid UserId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnTrueGivenStoredMembership()
    {
        // Arrange
        var client = new InMemoryKvClient();
        await SeedAsync(client, TenantId, UserId);
        var reader = new FitzTenantMembershipDirectoryReader(client);

        // Act
        var isMember = await reader.IsMemberAsync(TenantId.ToString(), UserId, CancellationToken.None);

        // Assert
        Assert.True(isMember);
    }

    [Fact]
    public async Task ShouldReturnFalseGivenNoStoredMembership()
    {
        // Arrange
        var reader = new FitzTenantMembershipDirectoryReader(new InMemoryKvClient());

        // Act
        var isMember = await reader.IsMemberAsync(TenantId.ToString(), UserId, CancellationToken.None);

        // Assert
        Assert.False(isMember);
    }

    [Fact]
    public async Task ShouldRetainSuspensionHistoryButNotCountSuspendedMemberGivenSuspension()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var reader = new FitzTenantMembershipDirectoryReader(client);
        var memberId = Uuid.CreateVersion4();
        var actorMemberId = Uuid.CreateVersion4();
        var suspendedAt = DateTimeOffset.UtcNow;
        var identity = new CheckpointIdentity("TenantMembership",
            EventStreamPattern.ForPattern(TenantId.ToString()));
        await using (var batch = await reader.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await reader.ApplyAsync(new MemberRegistered(TenantId, memberId, UserId));
            await reader.ApplyAsync(new MemberSuspended(TenantId, memberId, UserId, actorMemberId,
                "Alex Admin", suspendedAt, "Employment ended."));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var isMember = await reader.IsMemberAsync(TenantId.ToString(), UserId);
        var membership = await reader.GetAsync(TenantId.ToString(), UserId);

        // Assert
        Assert.False(isMember);
        Assert.NotNull(membership);
        Assert.True(membership.IsSuspended);
        Assert.Equal(suspendedAt, membership.SuspendedAt);
        Assert.Equal(actorMemberId, membership.SuspendedByMemberId);
        Assert.Equal("Alex Admin", membership.SuspendedByDisplay);
        Assert.Equal("Employment ended.", membership.SuspensionReason);
    }

    [Fact]
    public async Task ShouldRetainDeprovisionHistoryButNotCountDeprovisionedMemberGivenTermination()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var reader = new FitzTenantMembershipDirectoryReader(client);
        var memberId = Uuid.CreateVersion4();
        var actorMemberId = Uuid.CreateVersion4();
        var deprovisionedAt = DateTimeOffset.UtcNow;
        var identity = new CheckpointIdentity("TenantMembership",
            EventStreamPattern.ForPattern(TenantId.ToString()));
        await using (var batch = await reader.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await reader.ApplyAsync(new MemberRegistered(TenantId, memberId, UserId));
            await reader.ApplyAsync(new MemberDeprovisioned(TenantId, memberId, UserId, actorMemberId,
                "Alex Admin", deprovisionedAt, "Access is no longer required."));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var isMember = await reader.IsMemberAsync(TenantId.ToString(), UserId);
        var membership = await reader.GetAsync(TenantId.ToString(), UserId);

        // Assert
        Assert.False(isMember);
        Assert.NotNull(membership);
        Assert.True(membership.IsDeprovisioned);
        Assert.Equal(deprovisionedAt, membership.DeprovisionedAt);
        Assert.Equal(actorMemberId, membership.DeprovisionedByMemberId);
        Assert.Equal("Alex Admin", membership.DeprovisionedByDisplay);
        Assert.Equal("Access is no longer required.", membership.DeprovisionReason);
    }

    [Fact]
    public async Task ShouldRetainDeprovisionHistoryGivenFreshMembershipEpisode()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var reader = new FitzTenantMembershipDirectoryReader(client);
        var memberId = Uuid.CreateVersion4();
        var actorMemberId = Uuid.CreateVersion4();
        var deprovisionedAt = DateTimeOffset.UtcNow;
        var identity = new CheckpointIdentity("TenantMembership",
            EventStreamPattern.ForPattern(TenantId.ToString()));
        await using (var batch = await reader.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await reader.ApplyAsync(new MemberRegistered(TenantId, memberId, UserId));
            await reader.ApplyAsync(new MemberDeprovisioned(TenantId, memberId, UserId, actorMemberId,
                "Alex Admin", deprovisionedAt, "Access is no longer required."));
            await reader.ApplyAsync(new MemberRegistered(TenantId, memberId, UserId, "firm_staff"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var isMember = await reader.IsMemberAsync(TenantId.ToString(), UserId);
        var membership = await reader.GetAsync(TenantId.ToString(), UserId);

        // Assert
        Assert.True(isMember);
        Assert.NotNull(membership);
        Assert.False(membership.IsDeprovisioned);
        Assert.Equal("firm_staff", membership.Affiliation);
        Assert.Equal(deprovisionedAt, membership.DeprovisionedAt);
        Assert.Equal(actorMemberId, membership.DeprovisionedByMemberId);
        Assert.Equal("Access is no longer required.", membership.DeprovisionReason);
    }

    [Fact]
    public async Task ShouldHideMembershipGivenDifferentTenant()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var otherTenantId = Uuid.CreateVersion4();
        await SeedAsync(client, otherTenantId, UserId);
        var reader = new FitzTenantMembershipDirectoryReader(client);

        // Act
        var isMember = await reader.IsMemberAsync(TenantId.ToString(), UserId, CancellationToken.None);

        // Assert
        Assert.False(isMember);
    }

    [Fact]
    public async Task ShouldBackfillMembershipsGivenPreIndexEvents()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var legacy = new KvDirectory<TenantMembershipView, Uuid>(
            "tenant-memberships", Bdgrz.Compliance.ComplianceCoreJsonContext.Default.TenantMembershipView,
            static view => view.UserId, static id => [id.ToString()], []);
        var route = await new TenantMembershipRouteReader(client).GetRouteAsync(tenantId);
        await using (var tx = await client.BeginAsync(route,
            KvDurability.Sync, KvMode.ReadWrite))
        {
            await legacy.InsertAsync(tx, new TenantMembershipView(userId, tenantId));
            await tx.CommitAsync();
        }

        // Act
        var reader = new FitzTenantMembershipDirectoryReader(client);

        // Assert
        Assert.True(await reader.IsMemberAsync(tenantId.ToString(), userId));
        var page = await reader.ListAsync(tenantId, 50, null);

        var member = Assert.Single(page.Items);
        Assert.Equal(userId, member.UserId);
        Assert.Equal("client_personnel", member.Affiliation);
    }

    static async Task SeedAsync(InMemoryKvClient client, Uuid tenantId, Uuid userId)
    {
        var repository = new FitzTenantMembershipDirectoryReader(client);
        var identity = new CheckpointIdentity("TenantMembership", EventStreamPattern.ForPattern(tenantId.ToString()));
        await using var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await repository.ApplyAsync(new MemberRegistered(tenantId, Uuid.CreateVersion4(), userId));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class TenantMembershipRouteReader(IKvClient client)
        : FitzKvProjectionStore(client, TenantMembershipDirectoryKeys.Route, "TenantMembership")
    {
        public async Task<string> GetRouteAsync(Uuid tenantId)
        {
            await using var tx = await BeginReadAsync(tenantId.ToString());
            return tx.Route;
        }
    }
}
