using System.Security.Claims;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;

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
        await using var fixture = new StoreFixture();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var actor = Actor(userId);
        var control = new ControlDraft(tenantId,
            ControlDraft.IdFor(tenantId, programId, "AC-GATED"));
        Assert.True(control.Create(programId, Uuid.CreateVersion4(), "AC-GATED", Content(),
            RbacIds.Member(tenantId, userId), "Author", DateTimeOffset.UtcNow).IsSuccess);
        var request = new DiscardControlDraft(tenantId, programId, control.Id, 1,
            "The reader rollout is not complete.");
        var context = new FixedRequestContext<DiscardControlDraft>(request, actor,
            Uuid.CreateVersion4());
        await fixture.Repository.SaveAsync(control, context, CancellationToken.None);
        var handler = new DiscardControlDraftHandler(fixture.Repository,
            new ControlDraftDiscardReleaseGate(false), TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var source = await fixture.Repository.HydrateAsync(
            new ControlDraft(tenantId, control.Id), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.True(source.IsVisible);
        Assert.Equal(1, source.Revision);
    }

    [Fact]
    public async Task ShouldReplayExistingCreateWithoutRevalidatingApplicabilityGivenSameRequest()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var actor = Actor(userId);
        var content = new ControlDraftContent("Access review", "Review access",
            "Management reviews access", "The security lead reviews access quarterly.",
            ["Dated review record"], null,
            [new ControlApplicabilityReference(Uuid.CreateVersion4(), "system_instance",
                "GitHub production", Uuid.CreateVersion4(),
                "Privileged access is administered there.", false)]);
        var program = new ComplianceProgram(tenantId, programId);
        Assert.Null(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
            RbacIds.Member(tenantId, userId), "Author", DateTimeOffset.UtcNow));
        var request = new CreateControlDraft(tenantId, programId, "AC-RETRY", content);
        var context = new FixedRequestContext<CreateControlDraft>(request, actor, requestId);
        await fixture.Repository.SaveAsync(program, context, CancellationToken.None);
        var control = new ControlDraft(tenantId,
            ControlDraft.IdFor(tenantId, programId, request.Identifier));
        Assert.True(control.Create(programId, requestId, request.Identifier, content,
            RbacIds.Member(tenantId, userId), "Author", DateTimeOffset.UtcNow).IsSuccess);
        await fixture.Repository.SaveAsync(control, context, CancellationToken.None);
        var validator = new FailingApplicabilityValidator();
        var handler = new CreateControlDraftHandler(fixture.Repository, fixture.Repository,
            validator, TimeProvider.System);

        // Act
        var replay = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(control.Id, replay.Value.ControlId);
        Assert.Equal(0, validator.Calls);
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
            new Claim("email", "author@example.com")], "BdgrzSession"));

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

    sealed class FixedRequestContext<TRequest>(TRequest request, ClaimsPrincipal actor,
        Uuid requestId) : IRequestContext<TRequest>
    {
        public TRequest Request { get; } = request;
        public ClaimsPrincipal Actor => actor;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId { get; } = requestId;
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid? CausationId => null;
        public Uuid CauseId => RequestId;
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public RequestInvocation Invocation { get; } = new DirectInvocation();
    }
}
