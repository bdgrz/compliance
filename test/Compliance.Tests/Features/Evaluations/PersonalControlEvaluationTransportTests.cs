using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Readiness;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Evaluations;

public sealed class PersonalControlEvaluationTransportTests
{
    [Theory]
    [InlineData("submit", "direct")]
    [InlineData("submit", "mcp")]
    [InlineData("review", "direct")]
    [InlineData("review", "mcp")]
    [InlineData("submit", "http")]
    [InlineData("review", "http")]
    public async Task ShouldEnforcePersonalTransportGivenValidControlEvaluationDecision(string operation, string transport)
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var evaluation = await ControlEvaluationTests.RecordAllAsync(source, await ControlEvaluationTests.StartAsync(source));
        if (operation == "review")
            evaluation = await source.AsAsync(source.OwnerUserId, ControlEvaluationTests.Submit(source, evaluation));
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider,
            source.TenantId, source.ProgramId, source.Permissions);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ControlEvaluationLedger(source.TenantId, source.ProgramId));
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = operation == "submit" ? source.OwnerUserId : source.ApproverUserId;
        var context = PersonalReadinessClosureTransportTests.Context(actor, transport);

        // Act
        var result = operation == "submit"
            ? await bus.DispatchAsync(ControlEvaluationTests.Submit(source, evaluation), context, CancellationToken.None)
            : await bus.DispatchAsync(ControlEvaluationTests.Review(source, evaluation, "accepted"), context, CancellationToken.None);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ControlEvaluationLedger(source.TenantId, source.ProgramId));

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(evaluation.Revision + 1, result.Value.Revision);
            Assert.Equal(source.Member(actor), operation == "submit" ? result.Value.EvaluatorMemberId : result.Value.LatestReview!.ReviewerMemberId);
            Assert.True(after.CommittedStreamPosition > before.CommittedStreamPosition);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        }
    }
    [Theory]
    [InlineData("evaluator", "Only the evaluator may record this evaluation.")]
    [InlineData("manager", "Only a Compliance Lead or Org Admin for the program may review an evaluation.")]
    [InlineData("self", "The evaluator cannot review their own evaluation without an approved, active separation-of-duties waiver.")]
    public async Task ShouldPreserveEvaluationAuthorityGivenNativeHttpConflictingActor(string refusal, string message)
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var evaluator = refusal == "self" ? source.LeadUserId : source.OwnerUserId;
        var evaluation = await ControlEvaluationTests.RecordAllAsync(source, await ControlEvaluationTests.StartAsync(source, evaluator), evaluator);
        if (refusal != "evaluator")
            evaluation = await source.AsAsync(evaluator, ControlEvaluationTests.Submit(source, evaluation));
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider,
            source.TenantId, source.ProgramId, source.Permissions);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ControlEvaluationLedger(source.TenantId, source.ProgramId));

        // Act
        var result = refusal == "evaluator"
            ? await HttpAsync(provider, source.OutsiderUserId, ControlEvaluationTests.Submit(source, evaluation), RequestErrorKind.Forbidden)
            : await HttpAsync(provider, evaluator, ControlEvaluationTests.Review(source, evaluation, "accepted"), RequestErrorKind.Forbidden);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ControlEvaluationLedger(source.TenantId, source.ProgramId));

        // Assert
        Assert.Equal(message, result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    internal static Task<Result<T>> HttpAsync<T>(IServiceProvider provider, Uuid actor,
        IRequest<T> request, RequestErrorKind? expectedFailure = null) =>
        PersonalOccurrenceProofTransportTests.HttpAsync(provider, actor, request, expectedFailure);

}
