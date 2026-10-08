using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Remediation;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Readiness;

public sealed class PersonalReadinessClosureTransportTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    public async Task ShouldRefuseReadinessSignOffGivenNonHttpInvocation(string transport)
    {
        // Arrange
        var source = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var assessment = await source.RunAsync(0);
        await using var provider = await ComposeAsync(source.Provider, source.TenantId, source.ProgramId);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ReadinessLedger(source.TenantId, source.ProgramId));
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new DecideReadiness(source.TenantId, source.ProgramId, assessment.AssessmentId,
                assessment.Revision, "do_not_proceed", "Keep remediating the retained gaps."),
            Context(source.DeciderUserId, transport), CancellationToken.None);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ReadinessLedger(source.TenantId, source.ProgramId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
        Assert.Null(after.FindDecision(assessment.AssessmentId));
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    public async Task ShouldRefuseFindingVerificationGivenNonHttpInvocation(string transport)
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var finding = await FindingTests.RaiseWithCompletedActionAsync(source);
        await using var provider = await ComposeAsync(source.Provider, source.TenantId, source.ProgramId);
        var before = await ProgramManagementServices.HydrateAsync(provider, new RemediationLedger(source.TenantId, source.ProgramId));
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            FindingTests.Close(source, finding), Context(source.ApproverUserId, transport), CancellationToken.None);
        var after = await ProgramManagementServices.HydrateAsync(provider, new RemediationLedger(source.TenantId, source.ProgramId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
        Assert.Null(after.Read(finding.FindingId, DateTimeOffset.UtcNow)!.Closure);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRetainAttributedReadinessSignOffGivenNativeHttp()
    {
        // Arrange
        var source = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var assessment = await source.RunAsync(0);
        await using var provider = await ComposeAsync(source.Provider, source.TenantId, source.ProgramId);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new DecideReadiness(source.TenantId, source.ProgramId, assessment.AssessmentId,
                assessment.Revision, "do_not_proceed", "Continue remediation."),
            Context(source.DeciderUserId, "http"), CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new ReadinessLedger(source.TenantId, source.ProgramId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(result.Value, retained.FindDecision(assessment.AssessmentId));
        Assert.Equal(RbacIds.Member(source.TenantId, source.DeciderUserId), result.Value.DeciderMemberId);
    }

    [Fact]
    public async Task ShouldRetainAttributedFindingClosureGivenNativeHttp()
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var finding = await FindingTests.RaiseWithCompletedActionAsync(source);
        await using var provider = await ComposeAsync(source.Provider, source.TenantId, source.ProgramId);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            FindingTests.Close(source, finding), Context(source.ApproverUserId, "http"), CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new RemediationLedger(source.TenantId, source.ProgramId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("closed", result.Value.Status);
        Assert.Equal(source.ApproverMemberId, result.Value.Closure!.CloserMemberId);
        Assert.Equal(result.Value.Closure, retained.Read(finding.FindingId, DateTimeOffset.UtcNow)!.Closure);
    }

    [Fact]
    public async Task ShouldPreserveRunnerSeparationGivenNativeHttpReadiness()
    {
        // Arrange
        var source = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var assessment = await source.RunAsync(0);
        await using var provider = await ComposeAsync(source.Provider, source.TenantId, source.ProgramId);

        // Act
        var result = await DecideHttpAsync(provider, source.RunnerUserId,
            new DecideReadiness(source.TenantId, source.ProgramId, assessment.AssessmentId,
                assessment.Revision, "do_not_proceed", "I ran the assessment."), RequestErrorKind.Forbidden);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new ReadinessLedger(source.TenantId, source.ProgramId));

        // Assert
        Assert.Contains("ran an assessment", result.Error!.Message, StringComparison.Ordinal);
        Assert.Null(retained.FindDecision(assessment.AssessmentId));
    }

    [Fact]
    public async Task ShouldPreserveOwnerSeparationGivenNativeHttpClosure()
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var finding = await FindingTests.RaiseWithCompletedActionAsync(source, source.LeadMemberId);
        await using var provider = await ComposeAsync(source.Provider, source.TenantId, source.ProgramId);

        // Act
        var result = await CloseHttpAsync(provider, source.LeadUserId, FindingTests.Close(source, finding), RequestErrorKind.Forbidden);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new RemediationLedger(source.TenantId, source.ProgramId));

        // Assert
        Assert.Contains("owner cannot verify and close their own remediation", result.Error!.Message, StringComparison.Ordinal);
        Assert.Null(retained.Read(finding.FindingId, DateTimeOffset.UtcNow)!.Closure);
    }

    internal static async Task<Result<FindingView>> CloseHttpAsync(IServiceProvider provider, Uuid actor,
        CloseFinding request, RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            Context(actor, "http"), CancellationToken.None);
        if (expectedFailure is { } error)
            Assert.Equal(error, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    internal static async Task<Result<ReadinessDecisionView>> DecideHttpAsync(IServiceProvider provider, Uuid actor,
        DecideReadiness request, RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            Context(actor, "http"), CancellationToken.None);
        if (expectedFailure is { } error)
            Assert.Equal(error, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    internal static RequestDispatchContext Context(Uuid actor, string transport) => new(
        ProgramManagementServices.Actor(actor), transport == "http"
            ? new HttpInvocation("POST", "/synthetic/personal/decision", "/synthetic/personal/decision", "synthetic")
            : transport == "mcp" ? new McpInvocation("synthetic.personal.decision") : new DirectInvocation());

    internal static async Task<ServiceProvider> ComposeAsync(ServiceProvider source, Uuid tenant, Uuid programId)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance"
        }).Build(), developerAuthentication: true);
        var store = source.GetRequiredService<IEventStore>();
        services.AddSingleton(store);
        services.AddSingleton<IDomainEventReader>((IDomainEventReader)store);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
            new RecordingPermissionAuthorizer(allowed: true)));
        services.AddSingleton<ITenantActivity, ActiveTenant>();
        services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await ProgramManagementServices.SeedAsync(provider, new ComplianceProgram(tenant, programId), program =>
        {
            if (!program.IsCreated)
                Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                    Uuid.CreateVersion4(), "Client manager", DateTimeOffset.UtcNow));
            return Result.Success;
        });
        return provider;
    }
}
