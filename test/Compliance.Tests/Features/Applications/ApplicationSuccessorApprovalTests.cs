using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationSuccessorApprovalTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldDenyApprovalGivenMcpInvocation()
    {
        // Arrange
        var scenario = Scenario.Create();
        var executor = new NoWriteExecutor();
        var handler = scenario.Handler(executor, IncompleteRelationshipImpactReader());
        var context = scenario.Context(new McpInvocation("synthetic.application.successor.approve"));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.False(executor.Called);
        Assert.Equal(scenario.SourceEventCount, scenario.PendingSourceEvents());
    }

    [Fact]
    public async Task ShouldBlockApprovalWithoutAppendGivenPendingApplicationRelationshipImpact()
    {
        // Arrange
        var scenario = Scenario.Create();
        var executor = new NoWriteExecutor();
        var handler = scenario.Handler(executor, IncompleteRelationshipImpactReader());
        var context = scenario.Context(new HttpInvocation("POST", "/synthetic/successor/approve",
            "/synthetic/successor/approve", "synthetic"));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.False(executor.Called);
        Assert.Equal(scenario.SourceEventCount, scenario.PendingSourceEvents());
    }

    static ImpactReader IncompleteRelationshipImpactReader() => new(new ApplicationChangePreview(
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, "retire", [], [],
        ["application_relationships"], false)
    {
        ImpactDigest = new string('a', 64),
    });

    sealed class Scenario
    {
        readonly ClaimsPrincipal _actor;

        Scenario(DeclaredApplication source, DeclaredApplication successor,
            Uuid tenantId, Uuid predecessorId, Uuid successorId, Uuid memberId,
            ClaimsPrincipal actor)
        {
            Source = source;
            Successor = successor;
            TenantId = tenantId;
            PredecessorId = predecessorId;
            SuccessorId = successorId;
            MemberId = memberId;
            _actor = actor;
            SourceEventCount = PendingSourceEvents();
        }

        DeclaredApplication Source { get; }
        DeclaredApplication Successor { get; }
        Uuid TenantId { get; }
        Uuid PredecessorId { get; }
        Uuid SuccessorId { get; }
        Uuid MemberId { get; }
        public int SourceEventCount { get; }

        public static Scenario Create()
        {
            var tenantId = Uuid.CreateVersion4();
            var predecessorId = Uuid.CreateVersion4();
            var successorId = Uuid.CreateVersion4();
            var userId = Uuid.CreateVersion4();
            var proposerUserId = Uuid.CreateVersion4();
            var memberId = RbacIds.Member(tenantId, proposerUserId);
            var source = new DeclaredApplication(tenantId, predecessorId);
            var successor = new DeclaredApplication(tenantId, successorId);
            Assert.True(source.Declare("Payroll", "Payroll processing", null, memberId,
                "Preparer", Now).IsSuccess);
            Assert.True(successor.Declare("Payroll successor", "Payroll processing", null,
                RbacIds.Member(tenantId, userId), "Partner", Now).IsSuccess);
            Assert.True(source.RecordRelationship(successorId, 1, 1, "replaces", memberId,
                "Preparer", Now).IsSuccess);
            var actor = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())],
                "BdgrzSession"));
            return new Scenario(source, successor, tenantId, predecessorId, successorId,
                memberId, actor);
        }

        public int PendingSourceEvents() =>
            new AggregateScenario<DeclaredApplication>(Source).PendingEvents.Count;

        public ApproveApplicationSuccessorHandler Handler(IAggregateExecutor executor,
            IApplicationChangeImpactReader impactReader) => new(executor,
            new ApplicationReader(Source, Successor), impactReader, TimeProvider.System);

        public InvocationContext<ApproveApplicationSuccessor> Context(RequestInvocation invocation) =>
            new(new ApproveApplicationSuccessor(
                TenantId, PredecessorId, SuccessorId, 1, 1, 1, new string('a', 64)),
                _actor, invocation);
    }

    sealed class ApplicationReader(params DeclaredApplication[] applications) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var current = applications.FirstOrDefault(application =>
                application.Stream == aggregate.Stream);
            return ValueTask.FromResult(current is null
                ? aggregate
                : (TAggregate)(Aggregate)current);
        }
    }

    sealed class ImpactReader(ApplicationChangePreview preview) : IApplicationChangeImpactReader
    {
        public ValueTask<Result<ApplicationChangePreview>> ReadAsync(
            PreviewApplicationChange request, Uuid userId, CancellationToken ct) =>
            ValueTask.FromResult(Result<ApplicationChangePreview>.Success(preview with
            {
                TenantId = request.TenantId,
                ApplicationId = request.ApplicationId,
                ApplicationRevision = request.ExpectedApplicationRevision,
            }));
    }

    sealed class NoWriteExecutor : IAggregateExecutor
    {
        public bool Called { get; private set; }

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Called = true;
            throw new InvalidOperationException("An incomplete approval cannot append events.");
        }

        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Called = true;
            throw new InvalidOperationException("An incomplete approval cannot append events.");
        }
    }

    sealed class InvocationContext<TRequest>(TRequest request, ClaimsPrincipal actor,
        RequestInvocation invocation) : IRequestContext<TRequest>
    {
        public TRequest Request { get; } = request;
        public ClaimsPrincipal Actor => actor;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid? CausationId => null;
        public Uuid CauseId => RequestId;
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public RequestInvocation Invocation => invocation;
    }
}
