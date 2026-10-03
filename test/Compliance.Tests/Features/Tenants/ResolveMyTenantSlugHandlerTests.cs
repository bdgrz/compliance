using System.Globalization;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class ResolveMyTenantSlugHandlerTests
{
    [Fact]
    public async Task ShouldRedirectMemberToCurrentSlugGivenRetiredSlug()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var result = await scenario.ResolveAsync(scenario.TenantMemberId, scenario.TenantId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(scenario.TenantId, result.Value.TenantId);
        Assert.Equal("acme-next", result.Value.CurrentSlug);
        Assert.True(result.Value.Redirect);
    }

    [Fact]
    public async Task ShouldHideCurrentSlugGivenMemberOfAnotherTenantUsesRetiredSlug()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();

        // Act
        var result = await scenario.ResolveAsync(scenario.OtherTenantMemberId,
            scenario.OtherTenantId);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly IAggregateReader _reader;

        Scenario(ServiceProvider provider, AsyncServiceScope scope, IAggregateReader reader,
            Uuid tenantId, Uuid tenantMemberId, Uuid otherTenantId, Uuid otherTenantMemberId)
        {
            _provider = provider;
            _scope = scope;
            _reader = reader;
            TenantId = tenantId;
            TenantMemberId = tenantMemberId;
            OtherTenantId = otherTenantId;
            OtherTenantMemberId = otherTenantMemberId;
        }

        public Uuid TenantId { get; }
        public Uuid TenantMemberId { get; }
        public Uuid OtherTenantId { get; }
        public Uuid OtherTenantMemberId { get; }

        public async Task<Result<TenantSlugResolution>> ResolveAsync(Uuid userId,
            Uuid membershipTenantId)
        {
            var handler = new ResolveMyTenantSlugHandler(_reader,
                new ScopedMembershipDirectory(membershipTenantId, userId));
            var context = new RequestContext<ResolveMyTenantSlug>(
                new ResolveMyTenantSlug("acme"), Actor(userId));
            return await handler.HandleAsync(context, CancellationToken.None);
        }

        public static async Task<Scenario> CreateAsync()
        {
            var events = new InMemoryEventStore();
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(events);
            services.AddPortia();
            var provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true });
            var scope = provider.CreateAsyncScope();
            var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            var tenantId = Uuid.CreateVersion4();
            var tenantMemberId = Uuid.CreateVersion4();
            var otherTenantId = Uuid.CreateVersion4();
            var otherTenantMemberId = Uuid.CreateVersion4();

            var tenant = new Tenant(tenantId);
            Assert.True(tenant.Register(tenantMemberId, "Acme", "acme").IsSuccess);
            Assert.True(tenant.ConfirmSlug("acme").IsSuccess);
            Assert.True(tenant.RequestSlugChange("acme-next").IsSuccess);
            Assert.True(tenant.ConfirmSlug("acme-next").IsSuccess);
            await writer.SaveAsync(tenant, new RequestDispatchContext(RequestActor.System),
                CancellationToken.None);

            var oldSlug = new TenantSlug("acme");
            Assert.True(oldSlug.Register(tenantId).IsSuccess);
            Assert.True(oldSlug.Surrender(tenantId).IsSuccess);
            await writer.SaveAsync(oldSlug, new RequestDispatchContext(RequestActor.System),
                CancellationToken.None);
            var currentSlug = new TenantSlug("acme-next");
            Assert.True(currentSlug.Register(tenantId).IsSuccess);
            await writer.SaveAsync(currentSlug, new RequestDispatchContext(RequestActor.System),
                CancellationToken.None);
            await SeedMemberAsync(writer, tenantId, tenantMemberId);

            var otherTenant = new Tenant(otherTenantId);
            Assert.True(otherTenant.Register(otherTenantMemberId, "Other", "other").IsSuccess);
            Assert.True(otherTenant.ConfirmSlug("other").IsSuccess);
            await writer.SaveAsync(otherTenant,
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            var otherSlug = new TenantSlug("other");
            Assert.True(otherSlug.Register(otherTenantId).IsSuccess);
            await writer.SaveAsync(otherSlug, new RequestDispatchContext(RequestActor.System),
                CancellationToken.None);
            await SeedMemberAsync(writer, otherTenantId, otherTenantMemberId);

            return new Scenario(provider, scope, reader, tenantId, tenantMemberId, otherTenantId,
                otherTenantMemberId);
        }

        static async Task SeedMemberAsync(IAggregateWriter writer, Uuid tenantId, Uuid userId)
        {
            var member = new Member(tenantId, userId);
            Assert.True(member.Register().IsSuccess);
            await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System),
                CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class ScopedMembershipDirectory(Uuid tenantId, Uuid userId)
        : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string requestedTenantId,
            Uuid requestedUserId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(IsMember(requestedTenantId,
                requestedUserId)
                ? new TenantMembershipView(requestedUserId, tenantId)
                : null);

        public ValueTask<bool> IsMemberAsync(string requestedTenantId, Uuid requestedUserId,
            CancellationToken ct = default) =>
            ValueTask.FromResult(IsMember(requestedTenantId, requestedUserId));

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid requestedTenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));

        bool IsMember(string requestedTenantId, Uuid requestedUserId) =>
            Uuid.TryParse(requestedTenantId, CultureInfo.InvariantCulture, out var parsedTenantId) &&
            parsedTenantId == tenantId && requestedUserId == userId;
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));
}
