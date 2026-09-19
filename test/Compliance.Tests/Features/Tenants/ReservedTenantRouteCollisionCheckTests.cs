using Bdgrz.Compliance.Hosting;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class ReservedTenantRouteCollisionCheckTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldBlockTopLevelRouteGivenClaimedSlug(bool retired)
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var owner = Uuid.CreateVersion4();
        var slug = await fixture.Repository.HydrateAsync(new TenantSlug("reports"), CancellationToken.None);
        _ = slug.Register(owner);
        if (retired)
            _ = slug.Surrender(owner);

        // Act
        await fixture.Repository.SaveAsync(slug,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ReservedTenantRouteCollisionCheck.CheckAsync(
                fixture.Repository, ["reports"], CancellationToken.None));
    }

    [Fact]
    public async Task ShouldAllowStartupGivenUnclaimedRoute()
    {
        // Arrange
        await using var fixture = new StoreFixture();

        // Act
        var error = await Record.ExceptionAsync(() =>
            ReservedTenantRouteCollisionCheck.CheckAsync(
                fixture.Repository, ["reports"], CancellationToken.None));

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public async Task ShouldUseReservedRouteRegistryGivenHostedStartup()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var reservedSlug = new TenantSlug("login", allowReserved: true);
        DomainEvent historicalClaim = new TenantSlugRegistered(Uuid.CreateVersion4(), "login");
        historicalClaim.AttachMetadata(new DomainEventMetadata(
            Uuid.CreateVersion4(), reservedSlug.Id, 1, DateTimeOffset.UtcNow));
        await fixture.Store.AppendAsync(
            new EventStreamAddress("bdgrz", "tenant-slugs", reservedSlug.Id.ToString()),
            0, new List<DomainEvent> { historicalClaim });
        var services = new ServiceCollection();
        services.AddSingleton<IAggregateReader>(fixture.Repository);
        using var provider = services.BuildServiceProvider();

        // Act
        var hostedCheck = new ReservedTenantRouteCollisionCheck(
            provider.GetRequiredService<IServiceScopeFactory>());

        // Assert
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => hostedCheck.StartAsync(CancellationToken.None));

        Assert.Contains("reserved top-level route 'login'", failure.Message, StringComparison.Ordinal);
    }
}
