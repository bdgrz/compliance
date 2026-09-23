using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantInvitationConcurrencyTests
{
    [Fact]
    public async Task ShouldAcceptInvitationGivenConcurrentDeliveryOutcome()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        const string token = "single-use-invitation-token";
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var invitation = new TenantInvitation(tenantId, "member@example.com");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var now = DateTimeOffset.UtcNow;
        Assert.True(invitation.Invite("client_personnel", false, hash, now.AddDays(7), now,
            Uuid.CreateVersion4()).IsSuccess);
        await writer.SaveAsync(invitation, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var executor = new ConflictOnceExecutor(
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>());
        var handler = new AcceptTenantInvitationHandler(executor, TimeProvider.System);
        var request = new RequestContext<AcceptTenantInvitation>(
            new AcceptTenantInvitation(tenantId, "member@example.com", token), Actor(userId));

        // Act
        var result = await handler.HandleAsync(request, CancellationToken.None);
        var accepted = await reader.HydrateAsync(new TenantInvitation(tenantId,
            "member@example.com"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(accepted.IsAccepted);
        Assert.Equal(2, executor.Calls);
    }

    [Fact]
    public async Task ShouldIssueOneRecoverableAttemptGivenConcurrentDeliveryOutcome()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var administratorId = Uuid.CreateVersion4();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>()).Build();
        var keys = EmailChallengeTokenKeys.FromConfiguration(configuration, true);
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var executor = new ConflictOnceExecutor(
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>());
        var issuer = new TenantInvitationIssuer(executor, keys, TimeProvider.System);
        var request = new RequestContext<InviteOrganizationMember>(
            new InviteOrganizationMember(tenantId, "member@example.com", "compliance_management"),
            Actor(administratorId));

        // Act
        var result = await issuer.IssueAsync(request, tenantId, "member@example.com",
            "client_personnel", false, "compliance_management", administratorId,
            CancellationToken.None);
        var invitation = await reader.HydrateAsync(new TenantInvitation(tenantId,
            "member@example.com"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(invitation.CurrentDeliveryAttemptId);
        Assert.Equal(2, executor.Calls);
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "test"));

    sealed class ConflictOnceExecutor(IAggregateExecutor inner) : IAggregateExecutor
    {
        int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context,
            CancellationToken ct) where TAggregate : Aggregate
        {
            if (Interlocked.Increment(ref _calls) == 1)
                throw new EventStreamConcurrencyException("Concurrent delivery outcome.",
                    new InvalidOperationException());
            return inner.ExecuteAsync(aggregate, operation, context, ct);
        }

        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
            CancellationToken ct) where TAggregate : Aggregate
        {
            if (Interlocked.Increment(ref _calls) == 1)
                throw new EventStreamConcurrencyException("Concurrent delivery outcome.",
                    new InvalidOperationException());
            return inner.ExecuteAsync(aggregate, operation, context, ct);
        }
    }
}
