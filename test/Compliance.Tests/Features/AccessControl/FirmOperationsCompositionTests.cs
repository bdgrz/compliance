using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FirmOperationsCompositionTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();
    static ClaimsPrincipal Actor => new(new ClaimsIdentity([
        new Claim("iss", "bdgrz"), new Claim("sub", User.ToString())], "BdgrzSession"));

    [Fact]
    public async Task ShouldDispatchClientIndependenceHistoryGivenProductionCompositionAndCurrentClientAuthority()
    {
        // Arrange
        await using var provider = Compose(true, true, "client_personnel", false);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();

        // Act
        var result = await bus.SendAsync(new GetClientIndependenceHistory(Tenant), Actor);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(Tenant, result.Value!.TenantId);
        Assert.Empty(result.Value.Services);
    }

    [Theory]
    [InlineData(false, true, "client_personnel", false, RequestErrorKind.NotFound)]
    [InlineData(false, true, "client_personnel", true, RequestErrorKind.NotFound)]
    [InlineData(true, true, "firm_staff", false, RequestErrorKind.Forbidden)]
    [InlineData(true, false, "client_personnel", false, RequestErrorKind.Forbidden)]
    public async Task ShouldDenyClientHistoryGivenMissingCurrentOwningClientAuthority(bool member, bool permitted,
        string affiliation, bool platformOperator, RequestErrorKind expected)
    {
        // Arrange
        await using var provider = Compose(member, permitted, affiliation, platformOperator);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();

        // Act
        var result = await bus.SendAsync(new GetClientIndependenceHistory(Tenant), Actor);

        // Assert
        Assert.Equal(expected, result.Error?.Kind);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldDenyPlatformRulesGivenClientAdministratorWithoutCurrentOperatorAuthority()
    {
        // Arrange
        await using var provider = Compose(true, true, "client_personnel", false);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();

        // Act
        var result = await bus.SendAsync(new GetIndependenceRuleVersions(), Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldDenyForeignClientReadGivenCurrentOperatorAndDifferentOwningTenant()
    {
        // Arrange
        await using var provider = Compose(true, true, "client_personnel", true);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();

        // Act
        var result = await bus.SendAsync(new GetClientIndependenceHistory(Uuid.CreateVersion4()), Actor);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldPersistAndReadEntireDraftWorkflowGivenProductionComposedCurrentAuthorities()
    {
        // Arrange
        await using var provider = Compose(true, true, "client_personnel", true);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var lead = Uuid.CreateVersion4();
        var other = Uuid.CreateVersion4();
        var engagement = Uuid.CreateVersion4();
        var service = Uuid.CreateVersion4();
        var acknowledgement = Uuid.CreateVersion4();
        var content = new ServiceEngagementDraftContent("attest", "Synthetic scope", new DateOnly(2026, 1, 1), null, lead);
        var http = new RequestDispatchContext(Actor, new HttpInvocation("POST", "/synthetic", "/synthetic", "synthetic"));

        // Act
        Assert.True((await bus.SendAsync(new RegisterFirmStaff(lead, Uuid.CreateVersion4(), "attest", "Synthetic staff source", 0), Actor)).IsSuccess);
        Assert.True((await bus.SendAsync(new RegisterFirmStaff(other, Uuid.CreateVersion4(), "attest", "Synthetic staff source", 1), Actor)).IsSuccess);
        Assert.True((await bus.SendAsync(new SetFirmStaffStatus(other, false, "Synthetic disable", 2), Actor)).IsSuccess);
        Assert.True((await bus.SendAsync(new SetFirmStaffStatus(other, true, "Synthetic enable", 3), Actor)).IsSuccess);
        Assert.True((await bus.SendAsync(new ReviseIndependenceRules(0, new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")], "Synthetic rule source")), Actor)).IsSuccess);
        Assert.True((await bus.SendAsync(new CreateServiceEngagement(Tenant, engagement, 0, content), Actor)).IsSuccess);
        Assert.True((await bus.SendAsync(new AmendServiceEngagement(Tenant, engagement, 1, content with { Scope = "Revised scope" }), Actor)).IsSuccess);
        Assert.True((await bus.SendAsync(new RecordNonattestService(Tenant, service, 2, new NonattestServiceContent(engagement,
            "readiness", new DateOnly(2025, 12, 1), null, [lead], false, "Synthetic client source")), Actor)).IsSuccess);
        Assert.True((await bus.SendAsync(new EvaluateClientIndependence(Tenant, Uuid.CreateVersion4(), 3, 1, content.PeriodStart), Actor)).IsSuccess);
        Assert.True((await bus.DispatchAsync(new AcknowledgeEngagementManagement(Tenant, engagement, acknowledgement, 4, 2,
            [service], "I retain management responsibility."), http, CancellationToken.None)).IsSuccess);
        Assert.True((await bus.SendAsync(new ProposeServiceEngagementStaff(Tenant, engagement, other, 5), Actor)).IsSuccess);
        Assert.True((await bus.SendAsync(new WithdrawServiceEngagementStaffProposal(Tenant, engagement, other, 6,
            "Synthetic withdrawal"), Actor)).IsSuccess);
        Assert.True((await bus.SendAsync(new CloseServiceEngagement(Tenant, engagement, 7, "Synthetic cancellation"), Actor)).IsSuccess);
        var directory = await bus.SendAsync(new GetFirmStaffDirectory(), Actor);
        var roster = await bus.SendAsync(new ListAssignableFirmStaff(Tenant), Actor);
        var rules = await bus.SendAsync(new GetIndependenceRuleVersions(), Actor);
        var clientRules = await bus.SendAsync(new GetClientIndependenceRules(Tenant), Actor);
        var history = await bus.SendAsync(new GetClientIndependenceHistory(Tenant), Actor);
        var draft = await bus.SendAsync(new GetServiceEngagement(Tenant, engagement), Actor);
        var drafts = await bus.SendAsync(new ListServiceEngagements(Tenant), Actor);
        var lifecycle = await bus.SendAsync(new GetServiceEngagementHistory(Tenant, engagement), Actor);
        var acknowledgements = await bus.SendAsync(new GetEngagementManagementAcknowledgements(Tenant, engagement), Actor);

        // Assert
        Assert.True(directory.IsSuccess);
        Assert.True(roster.IsSuccess);
        Assert.Equal(2, directory.Value!.Staff.Count);
        Assert.Equal(2, roster.Value!.Staff.Count);
        Assert.False(Assert.Single(rules.Value!).IsRatified);
        Assert.Single(clientRules.Value!);
        Assert.Equal(8, history.Value!.Sequence);
        Assert.Equal(service, Assert.Single(history.Value.Services).ServiceRecordId);
        Assert.True(Assert.Single(history.Value.Evaluations).ProductionAcceptanceBlocked);
        Assert.Equal("closed", draft.Value!.Status);
        Assert.False(draft.Value.ProfessionalAccessGranted);
        Assert.Single(drafts.Value!);
        Assert.Equal(5, lifecycle.Value!.Count);
        var ack = Assert.Single(acknowledgements.Value!);
        Assert.Equal(User, ack.UserId);
        Assert.Equal(RbacIds.Member(Tenant, User).ToString(), ack.Actor.Id);
        Assert.Equal(service, Assert.Single(ack.CompleteServiceRecordIds));
    }

    static ServiceProvider Compose(bool member, bool permitted, string affiliation, bool platformOperator)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build();
        services.AddCompliance(configuration, developerAuthentication: true);
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(permitted));
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new OwningMembershipDirectory(member, affiliation));
        services.AddSingleton<IPlatformUserDirectoryReader>(new KnownUserDirectory());
        services.AddSingleton<IPlatformOperatorAccess>(new OperatorAccess(platformOperator));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed class KnownUserDirectory : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }

    sealed class OwningMembershipDirectory(bool member, string affiliation) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(member && tenantId == Tenant.ToString() && userId == User
                ? new TenantMembershipView(userId, Tenant, affiliation) : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(member && tenantId == Tenant.ToString() && userId == User);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class OperatorAccess(bool allowed) : IPlatformOperatorAccess
    {
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(allowed);
    }
}
