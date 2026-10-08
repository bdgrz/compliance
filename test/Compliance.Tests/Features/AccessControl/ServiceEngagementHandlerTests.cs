using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ServiceEngagementHandlerTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly Uuid Staff = Uuid.CreateVersion4();
    static readonly ActorReference Operator = ActorReference.ForPlatformOperator(User, "Synthetic operator");
    static ClaimsPrincipal Actor => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", User.ToString())], "BdgrzSession"));

    [Fact]
    public async Task ShouldRejectMalformedDraftGivenNullContent()
    {
        // Arrange
        var executor = new NoWriteExecutor();
        var handler = new CreateServiceEngagementHandler(executor,
            new ChangingDirectoryReader(Directory(), false), TimeProvider.System);
        var request = new CreateServiceEngagement(Tenant, Uuid.CreateVersion4(), 0, null!);

        // Act
        var result = await handler.HandleAsync(new RequestContext<CreateServiceEngagement>(request, Actor),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.False(executor.Called);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictWithoutGrantGivenStaffDisabledDuringDraftCommit()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton(TimeProvider.System);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var source = new ChangingDirectoryReader(Directory(), true);
        var handler = new CreateServiceEngagementHandler(
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), source, TimeProvider.System);
        var request = new CreateServiceEngagement(Tenant, Uuid.CreateVersion4(), 0,
            new ServiceEngagementDraftContent("advisory", "Synthetic scope", new DateOnly(2026, 1, 1), null, Staff));

        // Act
        var result = await handler.HandleAsync(new RequestContext<CreateServiceEngagement>(request, Actor),
            CancellationToken.None);
        var retained = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new IndependenceLedger(Tenant));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error?.IsTransient);
        var draft = Assert.Single(retained.Engagements);
        Assert.Equal("draft", draft.Status);
        Assert.False(draft.ProfessionalAccessGranted);
        var proposal = Assert.Single(draft.Staff);
        Assert.Equal("proposed", proposal.ProposalState);
        Assert.False(proposal.ProfessionalAccessGranted);
        Assert.Equal(2, source.Reads);
    }

    [Fact]
    public async Task ShouldRefuseUnknownCanonicalUserGivenDirectoryLag()
    {
        // Arrange
        var executor = new NoWriteExecutor();
        var handler = new RegisterFirmStaffHandler(executor, new MissingUserDirectory(), TimeProvider.System);
        var request = new RegisterFirmStaff(Staff, User, "advisory", "Synthetic staff provenance", 0);

        // Act
        var result = await handler.HandleAsync(new RequestContext<RegisterFirmStaff>(request, Actor),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.False(executor.Called);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public async Task ShouldRequireCurrentOperatorGivenStaffMetadataAdministration(bool isOperator,
        bool system, bool allowed)
    {
        // Arrange
        var authorizer = new FirmStaffAdministrationAuthorizer(new OperatorAccess(isOperator));
        var context = new RequestContext<IFirmStaffAdministrationRequest>(new GetFirmStaffDirectory(),
            system ? RequestActor.System : Actor);

        // Act
        var result = await authorizer.AuthorizeAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(allowed, result.IsSuccess);
    }

    sealed class OperatorAccess(bool allowed) : IPlatformOperatorAccess
    {
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(allowed);
    }

    static FirmStaffDirectory Directory()
    {
        var directory = new FirmStaffDirectory();
        Assert.True(directory.Register(Uuid.CreateVersion4(), Staff, User, "advisory", "Synthetic staff source",
            0, Operator, DateTimeOffset.UtcNow).IsSuccess);
        return directory;
    }

    sealed class ChangingDirectoryReader(FirmStaffDirectory directory, bool change) : IAggregateReader
    {
        public int Reads { get; private set; }
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Assert.IsType<FirmStaffDirectory>(aggregate);
            if (++Reads == 2 && change)
                Assert.True(directory.SetStatus(Uuid.CreateVersion4(), Staff, false, "Synthetic deactivation",
                    1, Operator, DateTimeOffset.UtcNow).IsSuccess);
            return ValueTask.FromResult((TAggregate)(Aggregate)directory);
        }
    }

    sealed class MissingUserDirectory : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(false);
    }

    sealed class NoWriteExecutor : IAggregateExecutor
    {
        public bool Called { get; private set; }
        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Called = true;
            throw new InvalidOperationException("No stream may be written.");
        }
        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            Called = true;
            throw new InvalidOperationException("No stream may be written.");
        }
    }
}
