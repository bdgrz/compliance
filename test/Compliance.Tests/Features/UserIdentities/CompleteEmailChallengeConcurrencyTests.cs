using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class CompleteEmailChallengeConcurrencyTests
{
    [Fact]
    public async Task ShouldVerifyGivenRepeatedConcurrentDeliveryConflicts()
    {
        // Arrange
        var ownerId = Uuid.CreateVersion4();
        var challengeId = Uuid.CreateVersion4();
        const string email = "owner@example.com";
        const string token = "test-verification-token";
        var now = DateTimeOffset.UtcNow;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton(TimeProvider.System);
        services.AddPortia()
            .AddRequestHandler<CompleteEmailChallengeHandler>()
            .AddRequestAuthorizer<EmailOwnershipAuthorizer>();
        services.AddScoped<ConcurrentDeliveryExecutor>(provider => new ConcurrentDeliveryExecutor(
            provider.GetRequiredService<IAggregateReader>(),
            provider.GetRequiredService<IAggregateWriter>(), email, challengeId,
            conflictsBeforeSuccess: 4));
        services.AddScoped<IAggregateExecutor>(provider =>
            provider.GetRequiredService<ConcurrentDeliveryExecutor>());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var address = new EmailAddress(email);
        Assert.True(address.Reserve(ownerId).IsSuccess);
        Assert.True(address.IssueChallenge(ownerId, challengeId, hash, now.AddMinutes(15), now).IsSuccess);
        await scope.ServiceProvider.GetRequiredService<IAggregateWriter>()
            .SaveAsync(address, new RequestDispatchContext(RequestActor.System));

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>()
            .SendAsync(new CompleteEmailChallenge(ownerId, email, token), RequestActor.System);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, scope.ServiceProvider.GetRequiredService<ConcurrentDeliveryExecutor>().Attempts);
        var verified = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new EmailAddress(email));
        Assert.True(verified.IsVerified);
        Assert.Equal("delivered", verified.DeliveryStatus);
    }

    sealed class ConcurrentDeliveryExecutor(IAggregateReader reader, IAggregateWriter writer,
        string email, Uuid challengeId, int conflictsBeforeSuccess) : IAggregateExecutor
    {
        public int Attempts { get; private set; }

        public async ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (++Attempts <= conflictsBeforeSuccess)
            {
                if (Attempts == 1)
                {
                    var address = await reader.HydrateAsync(new EmailAddress(email), ct);
                    Assert.True(address.RecordDeliverySent(challengeId, DateTimeOffset.UtcNow).IsSuccess);
                    await writer.SaveAsync(address, new RequestDispatchContext(RequestActor.System), ct);
                }
                throw new EventStreamConcurrencyException("The delivery outcome won the append race.");
            }

            return await new AggregateExecutor(reader, writer)
                .ExecuteAsync(aggregate, operation, context, ct);
        }

        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            throw new NotSupportedException();
    }
}
