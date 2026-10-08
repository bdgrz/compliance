using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Readiness;

public sealed class PersonalReadinessAnnotationTransportTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    [InlineData("http")]
    public async Task ShouldRequirePersonalFeedbackGivenRetainedAssessmentGap(string transport)
    {
        // Arrange
        var source = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var assessment = await source.RunAsync(0);
        var gap = assessment.Gaps[0];
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider, source.TenantId, source.ProgramId);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ReadinessLedger(source.TenantId, source.ProgramId));
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new AnnotateReadinessGap(source.TenantId, source.ProgramId, assessment.AssessmentId, gap.GapId, assessment.Revision, "Consider quarterly review."),
            PersonalReadinessClosureTransportTests.Context(source.DeciderUserId, transport), CancellationToken.None);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ReadinessLedger(source.TenantId, source.ProgramId));

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(before.CommittedStreamPosition + 1, after.CommittedStreamPosition);
            Assert.Equal(result.Value, after.FindAnnotation(result.Value.AnnotationId));
            Assert.Equal(RbacIds.Member(source.TenantId, source.DeciderUserId), result.Value.AuthorMemberId);
            Assert.Equal(result.Value.AuthorMemberId.ToString(), result.Value.AuthoredBy.Id);
            Assert.Equal(gap.GapId, result.Value.GapId);
            Assert.Equal(assessment.AssessmentId, result.Value.AssessmentId);
            Assert.Null(after.FindDecision(assessment.AssessmentId));
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        }
    }
    internal static async Task<Result<ReadinessAnnotationView>> HttpAsync(IServiceProvider provider, Uuid user,
        AnnotateReadinessGap request, RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            PersonalReadinessClosureTransportTests.Context(user, "http"), CancellationToken.None);
        if (expectedFailure is { } kind)
            Assert.Equal(kind, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    [Theory]
    [InlineData("gap", RequestErrorKind.NotFound, "The readiness gap was not found.")]
    [InlineData("revision", RequestErrorKind.Conflict, "The readiness ledger changed. Current revision: 1. Reload it and retry.")]
    [InlineData("grant", RequestErrorKind.Forbidden, "The actor may not manage this program.")]
    public async Task ShouldPreserveSourceRefusalGivenNativeHttpFeedback(string fault, RequestErrorKind kind, string message)
    {
        // Arrange
        var source = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var assessment = await source.RunAsync(0);
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider, source.TenantId, source.ProgramId,
            new RecordingPermissionAuthorizer(fault != "grant"));
        var before = await ProgramManagementServices.HydrateAsync(provider, new ReadinessLedger(source.TenantId, source.ProgramId));

        // Act
        var result = await HttpAsync(provider, source.DeciderUserId, new AnnotateReadinessGap(source.TenantId, source.ProgramId,
            assessment.AssessmentId, fault == "gap" ? Uuid.CreateVersion4() : assessment.Gaps[0].GapId, fault == "revision" ? 2 : 1, "Consider review."), kind);

        // Assert
        Assert.Equal(message, result.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, (await ProgramManagementServices.HydrateAsync(provider,
            new ReadinessLedger(source.TenantId, source.ProgramId))).CommittedStreamPosition);
    }

}
