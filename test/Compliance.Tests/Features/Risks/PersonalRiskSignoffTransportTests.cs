using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class PersonalRiskSignoffTransportTests
{
    [Theory]
    [InlineData("treatment", "direct")]
    [InlineData("treatment", "mcp")]
    [InlineData("submit", "direct")]
    [InlineData("submit", "mcp")]
    [InlineData("completion", "direct")]
    [InlineData("completion", "mcp")]
    public async Task ShouldRefusePersonalRiskSignoffGivenNonHttpInvocation(string action, string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, transport);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
        Assert.Equal(before.RevisionOf(fixture.Source.RiskId), retained.RevisionOf(fixture.Source.RiskId));
    }

    [Theory]
    [InlineData("treatment")]
    [InlineData("submit")]
    [InlineData("completion")]
    public async Task ShouldRetainAttributedRiskSignoffGivenNativeHttp(string action)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, "http");
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition + 1, retained.CommittedStreamPosition);
        Assert.Equal(before.RevisionOf(fixture.Source.RiskId) + 1, retained.RevisionOf(fixture.Source.RiskId));
        var view = retained.View(fixture.Source.RiskId, [], DateOnly.FromDateTime(DateTime.UtcNow));
        var expected = RbacIds.Member(fixture.Source.TenantId, fixture.Actor).ToString();
        if (action == "treatment")
        {
            var treatment = Assert.Single(view.ControlTreatments);
            Assert.Equal(expected, treatment.ReviewedBy!.Id);
            Assert.Equal("accepted", treatment.Status);
            Assert.Equal(fixture.Source.ControlVersionId, treatment.ControlVersionId);
        }
        else
        {
            var treatment = Assert.Single(view.TreatmentActions);
            var completion = Assert.Single(treatment.Completions);
            Assert.Equal([fixture.Evidence], completion.EvidenceRequestIds);
            if (action == "submit")
            {
                Assert.Equal(expected, completion.SubmittedBy.Id);
                Assert.Null(completion.ReviewOutcome);
                Assert.Equal("completion_submitted", treatment.Status);
            }
            else
            {
                Assert.Equal(expected, completion.ReviewedBy!.Id);
                Assert.Equal("accept", completion.ReviewOutcome);
                Assert.Equal("completed", treatment.Status);
            }
        }
    }

    [Theory]
    [InlineData("treatment", "proposer", "A control treatment proposer cannot review their own assertion.")]
    [InlineData("completion", "submitter", "Only the assigned reviewer may review this completion.")]
    [InlineData("completion", "owner", "Only the assigned reviewer may review this completion.")]
    [InlineData("completion", "submitter_missing_waiver", "The separation-of-duties waiver is not active for this member and completion.")]
    [InlineData("completion", "owner_missing_waiver", "The separation-of-duties waiver is not active for this member and completion.")]
    [InlineData("completion", "unassigned", "Only the assigned reviewer may review this completion.")]
    public async Task ShouldPreserveRiskParticipantSeparationGivenNativeHttp(string action, string conflict, string message)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        await fixture.UseConflictingActorAsync(conflict);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, "http");
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(message, result.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
    }

    internal static async Task<Result> HttpAsync(IServiceProvider provider, Uuid user, IRequest request,
        RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(user), Invocation("http")), CancellationToken.None);
        if (expectedFailure is { } kind)
            Assert.Equal(kind, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    internal static async Task<Result<T>> HttpAsync<T>(IServiceProvider provider, Uuid user, IRequest<T> request,
        RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(user), Invocation("http")), CancellationToken.None);
        if (expectedFailure is { } kind)
            Assert.Equal(kind, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    static RequestInvocation Invocation(string transport) => transport switch
    {
        "mcp" => new McpInvocation("synthetic.risk.signoff"),
        "direct" => new DirectInvocation(),
        _ => new HttpInvocation("POST", "/synthetic/risk/signoff", "/synthetic/risk/signoff", "synthetic")
    };

    sealed class Fixture(RiskGovernanceHandlerTests.Fixture source, ServiceProvider provider) : IAsyncDisposable
    {
        public RiskGovernanceHandlerTests.Fixture Source { get; } = source;
        Uuid _id;
        Uuid _evidence;
        Uuid _worker;
        Uuid? _missingWaiver;
        public Uuid Actor { get; private set; }
        public Uuid Evidence => _evidence;

        public static async Task<Fixture> CreateAsync(string action)
        {
            var source = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
            var fixture = new Fixture(source, await AttestRiskManagementWriteWallTests.ComposeAsync(source))
            {
                Actor = action == "submit" ? source.AssessorUserId : source.ApproverUserId
            };
            if (action == "treatment")
            {
                var proposal = await source.Scenario(source.AssessorUserId).When(new ProposeRiskControlTreatment(
                    source.TenantId, source.ProgramId, source.RiskId, 0, source.ControlId, source.ControlVersionId,
                    "Current approved control mitigates risk.")).ExpectSuccess();
                fixture._id = proposal.Value.TreatmentId;
            }
            else
            {
                var worker = await source.SeedMemberAsync();
                fixture._worker = worker.UserId;
                fixture._evidence = await source.SeedEvidenceAsync(true, worker);
                var added = await source.Scenario(source.AssessorUserId)
                    .When(source.AddAction(worker, [fixture._evidence], 0)).ExpectSuccess();
                fixture._id = added.Value.ActionId;
                if (action == "completion")
                {
                    var submission = await HttpAsync(source.Provider, source.AssessorUserId,
                        source.Submit(fixture._id, 1, [fixture._evidence]));
                    await source.AssignReviewAsync(submission.Value.SubmissionId);
                }
            }
            return fixture;
        }

        public Task UseConflictingActorAsync(string conflict)
        {
            Actor = conflict.StartsWith("owner", StringComparison.Ordinal) ? _worker : Source.AssessorUserId;
            if (conflict.EndsWith("missing_waiver", StringComparison.Ordinal))
                _missingWaiver = Uuid.CreateVersion4();
            return Task.CompletedTask;
        }

        public async Task<Result> DecideAsync(string action, string transport)
        {
            await using var scope = provider.CreateAsyncScope();
            var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
            var context = new RequestDispatchContext(ProgramManagementServices.Actor(Actor), Invocation(transport));
            if (action == "submit")
            {
                var result = await bus.DispatchAsync(Source.Submit(_id, 1, [_evidence]), context, CancellationToken.None);
                return result.IsSuccess ? Result.Success : Result.Failure(result.Error);
            }
            return action == "treatment"
                ? await bus.DispatchAsync(Source.ReviewTreatment(_id, "accept"), context, CancellationToken.None)
                : await bus.DispatchAsync(Source.ReviewAction(_id, 2, "accept") with { SeparationOfDutiesWaiverId = _missingWaiver }, context, CancellationToken.None);
        }

        public Task<RiskGovernanceLedger> ReadAsync() => ProgramManagementServices.HydrateAsync(provider,
            new RiskGovernanceLedger(Source.TenantId, Source.ProgramId));

        public async ValueTask DisposeAsync()
        {
            await provider.DisposeAsync();
            await Source.Provider.DisposeAsync();
        }
    }
}
