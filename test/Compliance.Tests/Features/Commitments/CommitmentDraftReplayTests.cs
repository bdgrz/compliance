using System.Security.Claims;
using Bdgrz.Compliance.Features.Commitments;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

public sealed class CommitmentDraftReplayTests
{
    [Fact]
    public async Task ShouldReplayOriginalCreateAndRejectNewDraftGivenRetiredService()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton(TimeProvider.System);
        services.AddPortia().AddRequestHandler<CreateCommitmentDraftHandler>();
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var handler = scope.ServiceProvider.GetRequiredService<CreateCommitmentDraftHandler>();
        var service = new ClientService(tenantId, serviceId);
        Assert.True(service.Create(programId, "Client service", "Purpose", "Operations",
            actorId, "Author", DateTimeOffset.UtcNow).IsSuccess);
        await writer.SaveAsync(service, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var original = new CreateCommitmentDraft(tenantId, programId, serviceId,
            "service_commitment", "SC-01", "Statement", "Context", "Source A");
        var requestId = Uuid.CreateVersion4();
        var context = new FixedRequestContext(original, requestId, actorId);
        var created = await handler.HandleAsync(context, CancellationToken.None);
        Assert.True(created.IsSuccess);
        service = await reader.HydrateAsync(new ClientService(tenantId, serviceId),
            CancellationToken.None);
        Assert.True(service.Retire(1, "Service ended", actorId, "Author",
            DateTimeOffset.UtcNow).IsSuccess);
        await writer.SaveAsync(service, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);

        // Act
        var replay = await handler.HandleAsync(context, CancellationToken.None);
        var newDraft = await handler.HandleAsync(new FixedRequestContext(original with
        {
            Identifier = "SC-02",
        }, Uuid.CreateVersion4(), actorId), CancellationToken.None);
        var changedReplay = await handler.HandleAsync(new FixedRequestContext(original with
        {
            Statement = "Changed statement",
        }, requestId, actorId), CancellationToken.None);
        var draft = await reader.HydrateAsync(new CommitmentDraft(tenantId,
            created.Value.DraftId), CancellationToken.None);
        var rejectedDraft = await reader.HydrateAsync(new CommitmentDraft(tenantId,
            CommitmentDraft.IdFor(tenantId, programId, "service_commitment", "SC-02")),
            CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(created.Value, replay.Value);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(newDraft.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(changedReplay.Error).Kind);
        Assert.Equal(1, draft.Revision);
        Assert.False(rejectedDraft.IsCreated);
    }

    sealed class FixedRequestContext(CreateCommitmentDraft request, Uuid requestId, Uuid actorId)
        : IRequestContext<CreateCommitmentDraft>
    {
        public CreateCommitmentDraft Request => request;
        public ClaimsPrincipal Actor { get; } = new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", actorId.ToString())],
            "BdgrzSession"));
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId => requestId;
        public Uuid CorrelationId => requestId;
        public Uuid? CausationId => null;
        public Uuid CauseId => requestId;
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public RequestInvocation Invocation { get; } = new DirectInvocation();
    }
}
