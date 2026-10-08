using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Readiness;

public sealed class PersonalTypeIEntryTransportTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    public async Task ShouldRefuseTypeIEntrySignOffGivenNonHttpInvocation(string transport)
    {
        // Arrange
        var source = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var assessment = await source.RunAsync(0);
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider, source.TenantId, source.ProgramId);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ReadinessLedger(source.TenantId, source.ProgramId));
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new DecideTypeIEntry(source.TenantId, source.ProgramId, assessment.AssessmentId,
                assessment.Revision, "defer", "Continue remediating before entry."),
            PersonalReadinessClosureTransportTests.Context(source.DeciderUserId, transport), CancellationToken.None);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ReadinessLedger(source.TenantId, source.ProgramId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
        Assert.Empty(after.TypeIEntryDecisions());
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRetainAttributedTypeIEntryDecisionGivenNativeHttp()
    {
        // Arrange
        var source = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var assessment = await source.RunAsync(0);
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider, source.TenantId, source.ProgramId);

        // Act
        var result = await DecideHttpAsync(provider, source.DeciderUserId,
            new DecideTypeIEntry(source.TenantId, source.ProgramId, assessment.AssessmentId,
                assessment.Revision, "defer", "Continue remediation."));
        var retained = await ProgramManagementServices.HydrateAsync(provider, new ReadinessLedger(source.TenantId, source.ProgramId));

        // Assert
        Assert.Equal(result.Value, retained.FindTypeIEntryDecision(result.Value.DecisionId));
        Assert.Equal(RbacIds.Member(source.TenantId, source.DeciderUserId), result.Value.DeciderMemberId);
    }

    internal static async Task<Result<TypeIEntryDecisionView>> DecideHttpAsync(IServiceProvider provider, Uuid actor,
        DecideTypeIEntry request, RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            PersonalReadinessClosureTransportTests.Context(actor, "http"), CancellationToken.None);
        if (expectedFailure is { } error)
            Assert.Equal(error, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }
}
