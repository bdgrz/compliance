using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class EmailReservationReactorTests
{
    [Fact]
    public async Task ShouldReserveEmailGivenUserRegistrationWithEmail()
    {
        // Arrange
        var owner = Uuid.CreateVersion4();
        var bus = new RecordingRequestBus();
        var reactor = new EmailReservationReactor(new InMemoryProjectionCheckpointStore(), bus);

        // Act
        await reactor.HandleAsync(new Context(new UserIdentityRegistered(owner, "oidc", "subject", "person@example.com")),
            CancellationToken.None);

        // Assert
        var request = Assert.IsType<ReserveEmail>(Assert.Single(bus.Dispatched));
        Assert.Equal(owner, request.UserId);
        Assert.Equal("person@example.com", request.EmailAddress);
    }

    [Fact]
    public async Task ShouldSkipReservationGivenRegistrationWithoutEmail()
    {
        // Arrange
        var bus = new RecordingRequestBus();
        var reactor = new EmailReservationReactor(new InMemoryProjectionCheckpointStore(), bus);

        // Act
        await reactor.HandleAsync(new Context(new UserIdentityRegistered(Uuid.CreateVersion4(), "oidc", "subject", null)),
            CancellationToken.None);

        // Assert
        Assert.Empty(bus.Dispatched);
    }

    [Fact]
    public async Task ShouldAllowLaterRegistrationsGivenOwnershipConflict()
    {
        // Arrange
        var bus = new RecordingRequestBus
        {
            DispatchResult = Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The email address belongs to another user.")),
        };
        var reactor = new EmailReservationReactor(new InMemoryProjectionCheckpointStore(), bus);

        // Act
        await reactor.HandleAsync(new Context(new UserIdentityRegistered(
            Uuid.CreateVersion4(), "oidc", "subject", "person@example.com")), CancellationToken.None);

        // Assert
        Assert.Single(bus.Dispatched);
    }

    [Fact]
    public async Task ShouldRemainRetryableGivenDispatchFailure()
    {
        // Arrange
        var bus = new RecordingRequestBus
        {
            DispatchResult = Result.Failure(new RequestError(RequestErrorKind.Validation, "Unexpected failure.")),
        };

        // Act
        var reactor = new EmailReservationReactor(new InMemoryProjectionCheckpointStore(), bus);

        // Assert
        await Assert.ThrowsAsync<ReactionCommandFailedException>(async () =>
            await reactor.HandleAsync(new Context(new UserIdentityRegistered(
                Uuid.CreateVersion4(), "oidc", "subject", "person@example.com")), CancellationToken.None));
    }

    sealed class RecordingRequestBus : IRequestBus
    {
        public List<IRequestBase> Dispatched { get; } = [];
        public Result DispatchResult { get; init; } = Result.Success;
        public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null) =>
            new(actor, metadata: metadata);
        public ValueTask<Result> AuthorizeAsync(IRequestBase request, RequestDispatchContext context,
            CancellationToken ct = default) => throw new NotSupportedException();
        public ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context,
            CancellationToken ct = default)
        {
            Dispatched.Add(request);
            return ValueTask.FromResult(DispatchResult);
        }
        public ValueTask<Result<TOut>> DispatchAsync<TOut>(IRequest<TOut> request, RequestDispatchContext context,
            CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(IStreamRequest<TOut> request,
            RequestDispatchContext context, CancellationToken ct = default) => throw new NotSupportedException();
    }

    sealed class Context(UserIdentityRegistered trigger) : IReactorContext<UserIdentityRegistered>
    {
        public UserIdentityRegistered Trigger { get; } = trigger;
        public DomainEventRecord Source { get; } = new(
            new EventStreamAddress("bdgrz", "user-identities", Uuid.CreateVersion4().ToString()),
            trigger, 0, EventCursor.Start);
        public ClaimsPrincipal Actor => RequestActor.System;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid CauseId { get; } = Uuid.CreateVersion4();
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    }
}
