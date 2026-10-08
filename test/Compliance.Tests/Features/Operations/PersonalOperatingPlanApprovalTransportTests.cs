using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Readiness;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class PersonalOperatingPlanApprovalTransportTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    [InlineData("http")]
    public async Task ShouldEnforcePersonalTransportGivenValidOperatingPlanApproval(string transport)
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var proposed = await source.ProposeAsync(0);
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider,
            source.TenantId, source.ProgramId, source.Permissions);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ControlOperationsLedger(source.TenantId, source.ProgramId));
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new ApproveControlOperatingPlan(source.TenantId, source.ProgramId, source.ControlId,
                proposed.Revision, proposed.PlanVersionId, "Approve independently."),
            PersonalReadinessClosureTransportTests.Context(source.ApproverUserId, transport), CancellationToken.None);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ControlOperationsLedger(source.TenantId, source.ProgramId));

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal("approved", result.Value.Status);
            Assert.Equal(source.ApproverMemberId.ToString(), result.Value.ApprovedBy!.Id);
            Assert.True(after.CommittedStreamPosition > before.CommittedStreamPosition);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
            Assert.Equal("pending_approval", after.FindPlan(source.ControlId, proposed.PlanVersionId)!.Status);
        }
    }
    [Fact]
    public async Task ShouldPreserveProposerSeparationGivenNativeHttpApproval()
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var proposed = await source.ProposeAsync(0);
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider,
            source.TenantId, source.ProgramId, source.Permissions);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ControlOperationsLedger(source.TenantId, source.ProgramId));

        // Act
        var result = await HttpAsync(provider, source.LeadUserId, new ApproveControlOperatingPlan(source.TenantId,
            source.ProgramId, source.ControlId, proposed.Revision, proposed.PlanVersionId, "Self approval."), RequestErrorKind.Forbidden);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ControlOperationsLedger(source.TenantId, source.ProgramId));

        // Assert
        Assert.Equal("The member who proposed an operating plan cannot approve it.", result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    internal static Task<Result<T>> HttpAsync<T>(IServiceProvider provider, Uuid actor,
        IRequest<T> request, RequestErrorKind? expectedFailure = null) =>
        PersonalOccurrenceProofTransportTests.HttpAsync(provider, actor, request, expectedFailure);

}
