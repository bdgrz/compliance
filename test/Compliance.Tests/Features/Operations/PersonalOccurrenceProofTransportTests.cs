using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Readiness;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class PersonalOccurrenceProofTransportTests
{
    [Theory]
    [InlineData("attest", "direct")]
    [InlineData("attest", "mcp")]
    [InlineData("correct", "direct")]
    [InlineData("correct", "mcp")]
    [InlineData("review", "direct")]
    [InlineData("review", "mcp")]
    [InlineData("attest", "http")]
    [InlineData("correct", "http")]
    [InlineData("review", "http")]
    public async Task ShouldEnforcePersonalTransportGivenValidOccurrenceProof(string operation, string transport)
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        await source.PlanAsync();
        var occurrence = (await source.OccurrencesAsync("missed"))[0];
        if (operation != "attest")
            occurrence = await source.AsAsync(source.OwnerUserId, source.Attest(occurrence));
        if (operation == "correct")
            occurrence = await source.AsAsync(source.ReviewerUserId, source.Review(occurrence, "returned"));
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider,
            source.TenantId, source.ProgramId, source.Permissions);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ControlOperationsLedger(source.TenantId, source.ProgramId));
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = operation == "review" ? source.ReviewerUserId : source.OwnerUserId;
        var context = PersonalReadinessClosureTransportTests.Context(actor, transport);

        // Act
        var result = operation switch
        {
            "attest" => await bus.DispatchAsync(source.Attest(occurrence), context, CancellationToken.None),
            "review" => await bus.DispatchAsync(source.Review(occurrence, "approved"), context, CancellationToken.None),
            _ => await bus.DispatchAsync(new CorrectControlAttestation(source.TenantId, source.ProgramId,
                source.ControlId, occurrence.OccurrenceId, occurrence.Revision, "complete",
                DateTimeOffset.UtcNow.AddMinutes(-1), null, null, "Added missing support.", null,
                OperationsFixture.FullSupport, "Reviewer requested support."), context, CancellationToken.None)
        };
        var after = await ProgramManagementServices.HydrateAsync(provider, new ControlOperationsLedger(source.TenantId, source.ProgramId));

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(operation == "attest" ? 2 : occurrence.Revision + 1, result.Value.Revision);
            Assert.True(after.CommittedStreamPosition > before.CommittedStreamPosition);
            Assert.Equal(source.Member(actor), operation == "review" ? result.Value.Reviews[^1].ReviewerMemberId :
                result.Value.Attestations[^1].RecorderMemberId);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        }
    }
    [Fact]
    public async Task ShouldPreserveRecorderSeparationGivenNativeHttpReview()
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        await source.PlanAsync(owner: new OperatingHolder("person", source.PersonId));
        var occurrence = (await source.OccurrencesAsync("missed"))[0];
        var submitted = await source.AsAsync(source.LeadUserId, source.Attest(occurrence, personId: source.PersonId));
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider,
            source.TenantId, source.ProgramId, source.Permissions);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ControlOperationsLedger(source.TenantId, source.ProgramId));

        // Act
        var result = await HttpAsync(provider, source.LeadUserId, source.Review(submitted, "approved"), RequestErrorKind.Forbidden);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ControlOperationsLedger(source.TenantId, source.ProgramId));

        // Assert
        Assert.Equal("The member who performed or recorded an attestation cannot review it.", result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveOperatingResponsibilityGivenNativeHttpAttestation()
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        await source.PlanAsync();
        var occurrence = (await source.OccurrencesAsync("missed"))[0];
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider,
            source.TenantId, source.ProgramId, source.Permissions);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ControlOperationsLedger(source.TenantId, source.ProgramId));

        // Act
        var result = await HttpAsync(provider, source.OutsiderUserId, source.Attest(occurrence), RequestErrorKind.Forbidden);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ControlOperationsLedger(source.TenantId, source.ProgramId));

        // Assert
        Assert.Equal("Only the control owner or backup owner may perform and attest to this occurrence.", result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    internal static async Task<Result<T>> HttpAsync<T>(IServiceProvider provider, Uuid actor,
        IRequest<T> request, RequestErrorKind? expectedFailure = null)
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
