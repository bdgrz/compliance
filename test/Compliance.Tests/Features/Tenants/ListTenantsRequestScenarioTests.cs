using System.Security.Claims;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class ListTenantsRequestScenarioTests
{
    static readonly Uuid OperatorId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldListPlatformTenantMetadataGivenConfiguredOperator()
    {
        // Arrange
        var tenant = new TenantView(Uuid.CreateVersion4(), "Acme", "acme", "suspended");
        var directory = new Directory(tenant);
        await using var provider = BuildProvider(directory);

        // Act
        var scenario = await RequestScenario.For(provider)
            .GivenActor(Actor(OperatorId))
            .When(new ListTenants(Limit: 1, Cursor: "after"))
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();

        // Assert
        Assert.Equal(tenant, Assert.Single(scenario.Value.Items));
        Assert.Equal(1, directory.LastLimit);
        Assert.Equal("after", directory.LastCursor);
    }

    [Fact]
    public async Task ShouldDenyPortfolioReadGivenUnconfiguredUser()
    {
        // Arrange
        var directory = new Directory();
        await using var provider = BuildProvider(directory);

        // Act
        await RequestScenario.For(provider)
            .GivenActor(Actor(Uuid.CreateVersion4()))
            .When(new ListTenants())
            .ExpectDenied(RequestErrorKind.Forbidden)
            .ExpectNotHandled();

        // Assert
        Assert.Equal(0, directory.ReadCount);
    }

    [Fact]
    public async Task ShouldDenyPortfolioReadGivenAnonymousActor()
    {
        // Arrange
        var directory = new Directory();
        await using var provider = BuildProvider(directory);

        // Act
        await RequestScenario.For(provider)
            .GivenActor(RequestActor.Anonymous)
            .When(new ListTenants())
            .ExpectDenied(RequestErrorKind.Unauthorized)
            .ExpectNotHandled();

        // Assert
        Assert.Equal(0, directory.ReadCount);
    }

    static ServiceProvider BuildProvider(Directory directory)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<ITenantDirectoryReader>(directory);
        services.AddSingleton<IPlatformOperatorAccess>(new FixedOperatorAccess(OperatorId));
        services.AddPortia()
            .AddRequestHandler<ListTenantsHandler>()
            .AddRequestAuthorizer<PlatformOperatorAuthorizer>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed class Directory(params TenantView[] tenants) : ITenantDirectoryReader
    {
        public int ReadCount { get; private set; }
        public int LastLimit { get; private set; }
        public string? LastCursor { get; private set; }

        public ValueTask<TenantView?> GetAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenants.FirstOrDefault(tenant => tenant.TenantId == tenantId));

        public ValueTask<Page<TenantView>> ListAsync(int limit, string? cursor,
            CancellationToken ct = default)
        {
            ReadCount++;
            LastLimit = limit;
            LastCursor = cursor;
            return ValueTask.FromResult(new Page<TenantView>(tenants, null));
        }
    }
}
