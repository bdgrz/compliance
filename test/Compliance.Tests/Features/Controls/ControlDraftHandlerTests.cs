using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlDraftHandlerTests
{
    [Fact]
    public void ShouldFailClosedGivenMissingDiscardReleaseConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var gate = ControlDraftDiscardReleaseGate.FromConfiguration(configuration);

        // Assert
        Assert.False(gate.IsEnabled);
    }

    [Fact]
    public void ShouldEnableDiscardGivenExplicitReleaseConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Compliance:Controls:DiscardEnabled"] = "true",
            })
            .Build();

        // Act
        var gate = ControlDraftDiscardReleaseGate.FromConfiguration(configuration);

        // Assert
        Assert.True(gate.IsEnabled);
    }

    [Fact]
    public async Task ShouldRejectDiscardBeforeReaderRolloutGivenDisabledReleaseGate()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(tenantId, programId, "AC-GATED");
        await using var provider = ProgramManagementServices.Build(
            new RecordingPermissionAuthorizer(allowed: true),
            portia => portia.AddRequestHandler<DiscardControlDraftHandler>(),
            services => services.AddSingleton(new ControlDraftDiscardReleaseGate(false)));
        await ProgramManagementServices.SeedAsync(provider, new ControlDraft(tenantId, controlId),
            control => control.Create(programId, Uuid.CreateVersion4(), "AC-GATED", Content(),
                RbacIds.Member(tenantId, userId), "Author", DateTimeOffset.UtcNow));

        await RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(userId))
            // Act
            .When(new DiscardControlDraft(tenantId, programId, controlId, 1,
                "The reader rollout is not complete."))
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectFailure(RequestErrorKind.Conflict);
        var source = await ProgramManagementServices.HydrateAsync(provider,
            new ControlDraft(tenantId, controlId));
        Assert.True(source.IsVisible);
        Assert.Equal(1, source.Revision);
    }

    [Fact]
    public async Task ShouldReplayExistingCreateWithoutRevalidatingApplicabilityGivenSameRequest()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var content = new ControlDraftContent("Access review", "Review access",
            "Management reviews access", "The security lead reviews access quarterly.",
            ["Dated review record"], null,
            [new ControlApplicabilityReference(Uuid.CreateVersion4(), "system_instance",
                "GitHub production", Uuid.CreateVersion4(),
                "Privileged access is administered there.", false)]);
        var request = new CreateControlDraft(tenantId, programId, "AC-RETRY", content);
        var controlId = ControlDraft.IdFor(tenantId, programId, request.Identifier);
        var validator = new FailingApplicabilityValidator();
        await using var provider = ProgramManagementServices.Build(
            new RecordingPermissionAuthorizer(allowed: true),
            portia => portia.AddRequestHandler<CreateControlDraftHandler>(),
            services => services.AddSingleton<IControlApplicabilityReferenceValidator>(validator));
        await ProgramManagementServices.SeedAsync(provider,
            new ComplianceProgram(tenantId, programId), program =>
            {
                Assert.Null(program.Create("SOC 2",
                    new ProgramPlan(null, null, null, null, null, null),
                    RbacIds.Member(tenantId, userId), "Author", DateTimeOffset.UtcNow));
                return Result.Success;
            });
        await ProgramManagementServices.SeedAsync(provider, new ControlDraft(tenantId, controlId),
            control => control.Create(programId, requestId, request.Identifier, content,
                RbacIds.Member(tenantId, userId), "Author", DateTimeOffset.UtcNow));

        await RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(userId))
            .GivenMetadata(new RequestMetadata(requestId, requestId, null))
            // Act
            .When(request)
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess(new ControlRegistration(controlId, "AC-RETRY", 1));
        Assert.Equal(0, validator.Calls);
    }

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews access", "The security lead reviews access quarterly.",
        ["Dated review record"]);

    sealed class FailingApplicabilityValidator : IControlApplicabilityReferenceValidator
    {
        public int Calls { get; private set; }

        public ValueTask<Result> ValidateAsync(Uuid tenantId, ControlDraftContent? content,
            CancellationToken ct = default)
        {
            Calls++;
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The external inventory is temporarily unavailable.")));
        }
    }
}
