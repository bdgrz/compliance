using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class AttestRiskManagementWriteWallTests
{
    [Theory]
    [InlineData("attest", false, true)]
    [InlineData("attest", true, false)]
    [InlineData("advisory", false, false)]
    public async Task ShouldApplyClientManagementWallGivenActualRiskTreatmentSource(string practice,
        bool otherClient, bool denied)
    {
        // Arrange
        var source = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var sourceProvider = source.Provider;
        await using var provider = await ComposeAsync(source);
        await AttestAssignmentHistoryFixture.SeedAsync(provider,
            otherClient ? Uuid.CreateVersion4() : source.TenantId, source.AssessorUserId,
            revoked: true, practice: practice);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var context = Context(source.AssessorUserId);

        // Act
        var result = await bus.DispatchAsync(new ProposeRiskControlTreatment(source.TenantId,
            source.ProgramId, source.RiskId, 0, source.ControlId, source.ControlVersionId,
            "Client treatment decision"), context, CancellationToken.None);
        var read = await bus.DispatchAsync(new GetRiskGovernance(source.TenantId, source.ProgramId,
            source.RiskId), context, CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess, read.Error?.Message);
        if (denied)
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("Attest", result.Error!.Message, StringComparison.Ordinal);
            Assert.Empty(read.Value!.ControlTreatments);
        }
        else
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Single(read.Value!.ControlTreatments);
        }
    }

    [Fact]
    public async Task ShouldDenyPersonalManagementRiskAcceptanceGivenActualAttestHistory()
    {
        // Arrange
        var source = await RiskGovernanceHandlerTests.Fixture.CreateAsync("accept");
        await using var sourceProvider = source.Provider;
        var residual = await source.Scenario(source.AssessorUserId).When(source.Residual(2)).ExpectSuccess();
        await using var provider = await ComposeAsync(source);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, source.TenantId, source.ApproverUserId);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            source.Accept(residual.Value.AssessmentId, null), Context(source.ApproverUserId), CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider,
            new RiskEvaluation(source.TenantId, source.RiskId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("Attest", result.Error!.Message, StringComparison.Ordinal);
        Assert.Empty(retained.ToView().Acceptances);
    }

    [Fact]
    public async Task ShouldPreserveIndependentRiskReviewGivenOrdinaryClientManagementAuthority()
    {
        // Arrange
        var source = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var sourceProvider = source.Provider;
        await using var provider = await ComposeAsync(source);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var proposed = await bus.DispatchAsync(new ProposeRiskControlTreatment(source.TenantId,
            source.ProgramId, source.RiskId, 0, source.ControlId, source.ControlVersionId,
            "Client treatment decision"), Context(source.AssessorUserId), CancellationToken.None);
        Assert.True(proposed.IsSuccess, proposed.Error?.Message);
        var review = source.ReviewTreatment(proposed.Value!.TreatmentId, "accept");

        // Act
        var selfReview = await bus.DispatchAsync(review, Context(source.AssessorUserId), CancellationToken.None);
        var independentReview = await bus.DispatchAsync(review, Context(source.ApproverUserId), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, selfReview.Error?.Kind);
        Assert.True(independentReview.IsSuccess, independentReview.Error?.Message);
    }

    [Fact]
    public async Task ShouldRetainPersonalRiskAcceptanceGivenOrdinaryIndependentHttpAuthority()
    {
        // Arrange
        var source = await RiskGovernanceHandlerTests.Fixture.CreateAsync("accept");
        await using var sourceProvider = source.Provider;
        var residual = await source.Scenario(source.AssessorUserId).When(source.Residual(2)).ExpectSuccess();
        await using var provider = await ComposeAsync(source);
        await using var scope = provider.CreateAsyncScope();
        var context = Context(source.ApproverUserId);

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            source.Accept(residual.Value.AssessmentId, null), context, CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider,
            new RiskEvaluation(source.TenantId, source.RiskId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(result.Value!.AcceptanceId, Assert.Single(retained.ToView().Acceptances).AcceptanceId);
    }

    [Theory]
    [InlineData("mcp")]
    [InlineData("direct")]
    public async Task ShouldDenyPersonalRiskAcceptanceGivenNonHttpInvocation(string transport)
    {
        // Arrange
        var source = await RiskGovernanceHandlerTests.Fixture.CreateAsync("accept");
        await using var sourceProvider = source.Provider;
        var residual = await source.Scenario(source.AssessorUserId).When(source.Residual(2)).ExpectSuccess();
        await using var provider = await ComposeAsync(source);
        await using var scope = provider.CreateAsyncScope();
        var context = new RequestDispatchContext(ProgramManagementServices.Actor(source.ApproverUserId),
            transport == "mcp" ? new McpInvocation("synthetic.risk.accept") : new DirectInvocation());

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            source.Accept(residual.Value.AssessmentId, null), context, CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider,
            new RiskEvaluation(source.TenantId, source.RiskId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(retained.ToView().Acceptances);
    }

    static RequestDispatchContext Context(Uuid userId) => new(ProgramManagementServices.Actor(userId),
        new HttpInvocation("POST", "/synthetic/risk-management", "/synthetic/risk-management", "synthetic"));

    internal static async Task<ServiceProvider> ComposeAsync(RiskGovernanceHandlerTests.Fixture source)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance"
        }).Build(), developerAuthentication: true);
        var events = source.Provider.GetRequiredService<IEventStore>();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships(source.TenantId));
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
            new RecordingPermissionAuthorizer(true)));
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await ProgramManagementServices.SeedAsync(provider, new ComplianceProgram(source.TenantId, source.ProgramId),
            program =>
            {
                Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                    RbacIds.Member(source.TenantId, source.AssessorUserId), "Client administrator", DateTimeOffset.UtcNow));
                return Result.Success;
            });
        return provider;
    }

    sealed class Memberships(Uuid owningTenant) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenant, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenant == owningTenant.ToString()
                ? new TenantMembershipView(userId, owningTenant, "client_personnel") : null);
        public ValueTask<bool> IsMemberAsync(string tenant, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenant == owningTenant.ToString());
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenant, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}
