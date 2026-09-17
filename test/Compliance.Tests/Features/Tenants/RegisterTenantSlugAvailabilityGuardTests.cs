using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class RegisterTenantSlugAvailabilityGuardTests
{
    [Fact]
    public async Task ShouldAllowAnUnclaimedSlug()
    {
        await using var fixture = new StoreFixture();
        var guard = new RegisterTenantSlugAvailabilityGuard(fixture.Repository);
        var context = Context(new RegisterTenant("Acme", "acme"));

        var result = await guard.GuardAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectASlugClaimedByAnotherTenant()
    {
        await using var fixture = new StoreFixture();
        var occupied = await fixture.Repository.HydrateAsync(new TenantSlug("acme"), CancellationToken.None);
        _ = occupied.Register(Uuid.CreateVersion4());
        await fixture.Repository.SaveAsync(
            occupied, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var guard = new RegisterTenantSlugAvailabilityGuard(fixture.Repository);
        var context = Context(new RegisterTenant("Acme", "acme"));

        var result = await guard.GuardAsync(context, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldAllowARetryThatAlreadyOwnsTheSlug()
    {
        await using var fixture = new StoreFixture();
        var context = Context(new RegisterTenant("Acme", "acme"));
        var owned = await fixture.Repository.HydrateAsync(new TenantSlug("acme"), CancellationToken.None);
        _ = owned.Register(context.RequestId);
        await fixture.Repository.SaveAsync(
            owned, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var guard = new RegisterTenantSlugAvailabilityGuard(fixture.Repository);

        var result = await guard.GuardAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldLetTheAggregateOwnValidationForAnInvalidSlug()
    {
        await using var fixture = new StoreFixture();
        var guard = new RegisterTenantSlugAvailabilityGuard(fixture.Repository);
        var context = Context(new RegisterTenant("Acme", "!!"));

        var result = await guard.GuardAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    static RequestContext<RegisterTenant> Context(RegisterTenant request) =>
        new(request, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession")));
}
