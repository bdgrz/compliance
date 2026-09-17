using System.Security.Claims;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

/// <summary>
///     Runs <see cref="ListMyTenants" /> through Portia's real composed lifecycle — confirms
///     <see cref="ListMyTenantsAuthorizer" /> is actually wired to it, and that the handler resolves
///     only the calling user's own memberships.
/// </summary>
public sealed class ListMyTenantsRequestScenarioTests
{
    static readonly Uuid CallerUserId = Uuid.CreateVersion4();
    static readonly Uuid OtherUserId = Uuid.CreateVersion4();
    static readonly Uuid TenantId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldDenyGivenAnActorWithoutABdgrzIdentity()
    {
        await using var provider = BuildProvider(new FakeTenantMembershipDirectoryReader(), new FakeTenantDirectoryReader());

        await RequestScenario.For(provider)
            .GivenActor(RequestActor.Anonymous)
            .When(new ListMyTenants())
            .ExpectDenied(RequestErrorKind.Unauthorized)
            .ExpectNotHandled();
    }

    [Fact]
    public async Task ShouldReturnOnlyTheCallersOwnTenants()
    {
        var memberships = new FakeTenantMembershipDirectoryReader();
        memberships.ByUser[CallerUserId] = [new TenantMembershipView(CallerUserId, TenantId)];
        memberships.ByUser[OtherUserId] = [new TenantMembershipView(OtherUserId, Uuid.CreateVersion4())];
        var tenants = new FakeTenantDirectoryReader();
        tenants.ById[TenantId] = new TenantView(TenantId, "Acme", "acme");
        await using var provider = BuildProvider(memberships, tenants);

        var scenario = await RequestScenario.For(provider)
            .GivenActor(BdgrzActor(CallerUserId))
            .When(new ListMyTenants())
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();

        var summary = Assert.Single(scenario.Value.Items);
        Assert.Equal(TenantId, summary.TenantId);
        Assert.Equal("Acme", summary.Name);
        Assert.Equal("acme", summary.Slug);
    }

    [Fact]
    public async Task ShouldSkipAMembershipWhoseTenantHasNotProjectedYet()
    {
        var memberships = new FakeTenantMembershipDirectoryReader();
        memberships.ByUser[CallerUserId] = [new TenantMembershipView(CallerUserId, TenantId)];
        var tenants = new FakeTenantDirectoryReader();
        await using var provider = BuildProvider(memberships, tenants);

        var scenario = await RequestScenario.For(provider)
            .GivenActor(BdgrzActor(CallerUserId))
            .When(new ListMyTenants())
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();

        Assert.Empty(scenario.Value.Items);
    }

    static ServiceProvider BuildProvider(
        ITenantMembershipDirectoryReader memberships, ITenantDirectoryReader tenants)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton(memberships);
        services.AddSingleton(tenants);
        services.AddPortia()
            .AddRequestHandler<ListMyTenantsHandler>()
            .AddRequestAuthorizer<ListMyTenantsAuthorizer>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    static ClaimsPrincipal BdgrzActor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed class FakeTenantMembershipDirectoryReader : ITenantMembershipDirectoryReader
    {
        public Dictionary<Uuid, List<TenantMembershipView>> ByUser { get; } = [];

        public ValueTask<Page<TenantMembershipView>> ListByUserAsync(
            Uuid userId,
            int? limit,
            string? cursor,
            bool descending,
            CancellationToken ct = default)
        {
            IReadOnlyList<TenantMembershipView> items = ByUser.TryGetValue(userId, out var memberships)
                ? memberships
                : [];
            return ValueTask.FromResult(new Page<TenantMembershipView>(items, null));
        }
    }

    sealed class FakeTenantDirectoryReader : ITenantDirectoryReader
    {
        public Dictionary<Uuid, TenantView> ById { get; } = [];

        public ValueTask<TenantView?> GetAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(ById.TryGetValue(tenantId, out var tenant) ? tenant : null);
    }
}
