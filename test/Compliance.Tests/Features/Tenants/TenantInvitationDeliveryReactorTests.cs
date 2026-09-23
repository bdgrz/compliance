using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantInvitationDeliveryReactorTests
{
    [Fact]
    public async Task ShouldRetrySameTokenGivenRestartAfterSendBeforeOutcome()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var attemptId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(7);
        var keys = Keys();
        var token = keys.DeriveInvitation(keys.ActiveKeyId, attemptId, tenantId,
            "member@example.com", expiresAt);
        var invited = Event(tenantId, attemptId, token, expiresAt, keys.ActiveKeyId);
        var services = new ServiceCollection();
        var store = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(store);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        await SeedAsync(writer, invited, now);
        var firstDelivery = new RecordingDelivery { CancelAfterSend = true };
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var firstWorker = new TenantInvitationDeliveryReactor(checkpoints, reader, writer,
            firstDelivery, keys, TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        firstDelivery.Cancellation = cancellation;

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await firstWorker.HandleAsync(new Context(invited), cancellation.Token));
        var pending = await reader.HydrateAsync(new TenantInvitation(tenantId,
            invited.EmailAddress), CancellationToken.None);
        var resumedDelivery = new RecordingDelivery();
        var restartedWorker = new TenantInvitationDeliveryReactor(checkpoints, reader, writer,
            resumedDelivery, keys, TimeProvider.System);
        await restartedWorker.HandleAsync(new Context(invited), CancellationToken.None);
        await restartedWorker.HandleAsync(new Context(invited), CancellationToken.None);
        var delivered = await reader.HydrateAsync(new TenantInvitation(tenantId,
            invited.EmailAddress), CancellationToken.None);
        ActorAttribution? outcomeActor = null;
        await foreach (var record in store.ReadAsync(
                           new TenantInvitation(tenantId, invited.EmailAddress).Stream,
                           0, CancellationToken.None))
        {
            if (record.Event is TenantInvitationDeliverySent)
                outcomeActor = record.Event.Metadata.Actor;
        }

        // Assert
        Assert.Equal("pending", pending.DeliveryStatus);
        Assert.Equal("delivered", delivered.DeliveryStatus);
        Assert.Equal((attemptId, token), Assert.Single(firstDelivery.Attempts));
        Assert.Equal((attemptId, token), Assert.Single(resumedDelivery.Attempts));
        Assert.Equal(new ActorAttribution("reactor:TenantInvitationDeliveryV1", "bdgrz.system"),
            outcomeActor);
        var serialized = JsonSerializer.Serialize(invited,
            ComplianceCoreJsonContext.Default.TenantMemberInvited);
        Assert.DoesNotContain(token, serialized, StringComparison.Ordinal);
        Assert.Contains(invited.TokenHash, serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldCheckpointFailedAttemptAndDeliverLaterInviteGivenSameTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var attemptId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(7);
        var keys = Keys();
        var token = keys.DeriveInvitation(keys.ActiveKeyId, attemptId, tenantId,
            "member@example.com", expiresAt);
        var invited = Event(tenantId, attemptId, token, expiresAt, keys.ActiveKeyId);
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        await SeedAsync(writer, invited, now);
        var delivery = new RecordingDelivery { FailFirst = true };
        var reactor = new TenantInvitationDeliveryReactor(new InMemoryProjectionCheckpointStore(),
            reader, writer, delivery, keys, TimeProvider.System);

        // Act
        await reactor.HandleAsync(new Context(invited), CancellationToken.None);
        var failed = await reader.HydrateAsync(new TenantInvitation(tenantId,
            invited.EmailAddress), CancellationToken.None);
        await reactor.HandleAsync(new Context(invited), CancellationToken.None);
        var nextEmail = "next@example.com";
        var nextId = Uuid.CreateVersion4();
        var nextToken = keys.DeriveInvitation(keys.ActiveKeyId, nextId, tenantId,
            nextEmail, expiresAt);
        var next = Event(tenantId, nextId, nextToken, expiresAt, keys.ActiveKeyId,
            nextEmail);
        await SeedAsync(writer, next, now);
        await reactor.HandleAsync(new Context(next), CancellationToken.None);
        var later = await reader.HydrateAsync(new TenantInvitation(tenantId,
            nextEmail), CancellationToken.None);

        // Assert
        Assert.Equal("failed", failed.DeliveryStatus);
        Assert.Equal("delivered", later.DeliveryStatus);
        Assert.Equal(2, delivery.Attempts.Count);
        Assert.Equal((attemptId, token), delivery.Attempts[0]);
        Assert.Equal((nextId, nextToken), delivery.Attempts[1]);
    }

    [Fact]
    public async Task ShouldCheckpointMissingKeyAndDeliverLaterInviteGivenSameTenant()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(7);
        var keys = Keys();
        var unavailable = Event(tenantId, Uuid.CreateVersion4(), "old-private-token",
            expiresAt, "retired", "old@example.com");
        var laterId = Uuid.CreateVersion4();
        var laterToken = keys.DeriveInvitation(keys.ActiveKeyId, laterId, tenantId,
            "later@example.com", expiresAt);
        var later = Event(tenantId, laterId, laterToken, expiresAt, keys.ActiveKeyId,
            "later@example.com");
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        await SeedAsync(writer, unavailable, now);
        await SeedAsync(writer, later, now);
        var delivery = new RecordingDelivery();
        var reactor = new TenantInvitationDeliveryReactor(
            new InMemoryProjectionCheckpointStore(), reader, writer, delivery, keys,
            TimeProvider.System);

        // Act
        await reactor.HandleAsync(new Context(unavailable), CancellationToken.None);
        await reactor.HandleAsync(new Context(later), CancellationToken.None);
        var failed = await reader.HydrateAsync(new TenantInvitation(tenantId,
            unavailable.EmailAddress), CancellationToken.None);
        var sent = await reader.HydrateAsync(new TenantInvitation(tenantId,
            later.EmailAddress), CancellationToken.None);

        // Assert
        Assert.Equal("failed", failed.DeliveryStatus);
        Assert.Equal("delivered", sent.DeliveryStatus);
        Assert.Equal((laterId, laterToken), Assert.Single(delivery.Attempts));
    }

    [Fact]
    public async Task ShouldSkipStaleAndFailLegacyGivenSupersededExpiredAndHistoricalAttempts()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var keys = Keys();
        var firstId = Uuid.CreateVersion4();
        var secondId = Uuid.CreateVersion4();
        var firstExpiry = now.AddDays(6);
        var secondExpiry = now.AddDays(7);
        var first = Event(tenantId, firstId, keys.DeriveInvitation(keys.ActiveKeyId,
            firstId, tenantId, "member@example.com", firstExpiry), firstExpiry, keys.ActiveKeyId);
        var secondToken = keys.DeriveInvitation(keys.ActiveKeyId, secondId, tenantId,
            "member@example.com", secondExpiry);
        var second = Event(tenantId, secondId, secondToken, secondExpiry, keys.ActiveKeyId);
        var store = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(store);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var invitation = new TenantInvitation(tenantId, first.EmailAddress);
        Assert.True(invitation.Invite(first.Affiliation, first.Administrator, first.TokenHash,
            first.ExpiresAt, now, first.InvitedBy, first.BuiltInRole, firstId,
            first.TokenKeyId).IsSuccess);
        Assert.True(invitation.Invite(second.Affiliation, second.Administrator, second.TokenHash,
            second.ExpiresAt, now, second.InvitedBy, second.BuiltInRole, secondId,
            second.TokenKeyId).IsSuccess);
        await writer.SaveAsync(invitation, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var delivery = new RecordingDelivery();
        var reactor = new TenantInvitationDeliveryReactor(new InMemoryProjectionCheckpointStore(),
            reader, writer, delivery, keys, TimeProvider.System);

        // Act
        await reactor.HandleAsync(new Context(first), CancellationToken.None);
        await reactor.HandleAsync(new Context(first with { ExpiresAt = now.AddSeconds(-1) }),
            CancellationToken.None);
        await reactor.HandleAsync(new Context(second), CancellationToken.None);

        // Assert
        Assert.Equal((secondId, secondToken), Assert.Single(delivery.Attempts));

        var historicalTenant = Uuid.CreateVersion4();
        var legacy = new TenantMemberInvited(historicalTenant, "legacy@example.com",
            "client_personnel", false, new string('A', 64), now.AddDays(7), Uuid.CreateVersion4());
        var historicalAggregate = new TenantInvitation(historicalTenant, legacy.EmailAddress);
        legacy.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), historicalAggregate.Id,
            1, now));
        await store.AppendAsync(new EventStreamAddress(historicalTenant.ToString(),
            "tenant-invitations", historicalAggregate.Id.ToString()), 0, [legacy]);
        await reactor.HandleAsync(new Context(legacy), CancellationToken.None);
        var historical = await reader.HydrateAsync(new TenantInvitation(historicalTenant,
            legacy.EmailAddress), CancellationToken.None);
        Assert.Equal("failed", historical.DeliveryStatus);
        Assert.Single(delivery.Attempts);
    }

    static EmailChallengeTokenKeys Keys()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Compliance:EmailDelivery:ActiveTokenKeyId"] = "current",
                ["Compliance:EmailDelivery:TokenKeys:current"] =
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            }).Build();
        return EmailChallengeTokenKeys.FromConfiguration(configuration, false);
    }

    static TenantMemberInvited Event(Uuid tenantId, Uuid attemptId, string token,
        DateTimeOffset expiresAt, string keyId, string emailAddress = "member@example.com") =>
        new(tenantId, emailAddress, "client_personnel", false,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            expiresAt, Uuid.CreateVersion4(), null, attemptId, keyId);

    static async Task SeedAsync(IAggregateWriter writer, TenantMemberInvited invited,
        DateTimeOffset now)
    {
        var invitation = new TenantInvitation(invited.TenantId, invited.EmailAddress);
        Assert.True(invitation.Invite(invited.Affiliation, invited.Administrator,
            invited.TokenHash, invited.ExpiresAt, now, invited.InvitedBy,
            invited.BuiltInRole, invited.DeliveryAttemptId, invited.TokenKeyId).IsSuccess);
        await writer.SaveAsync(invitation, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
    }

    sealed class RecordingDelivery : ITenantInvitationDelivery
    {
        public List<(Uuid AttemptId, string Token)> Attempts { get; } = [];
        public bool FailFirst { get; init; }
        public bool CancelAfterSend { get; init; }
        public CancellationTokenSource? Cancellation { get; set; }

        public ValueTask SendAsync(Uuid attemptId, Uuid tenantId, string emailAddress,
            string token, CancellationToken ct)
        {
            _ = tenantId;
            _ = emailAddress;
            Attempts.Add((attemptId, token));
            if (FailFirst && Attempts.Count == 1)
                throw new InvalidOperationException($"provider secret {token} for {emailAddress}");
            if (CancelAfterSend)
            {
                Cancellation!.Cancel();
                ct.ThrowIfCancellationRequested();
            }
            return ValueTask.CompletedTask;
        }
    }

    sealed class Context(TenantMemberInvited trigger) : IReactorContext<TenantMemberInvited>
    {
        public TenantMemberInvited Trigger { get; } = trigger;
        public DomainEventRecord Source { get; } = new(
            new EventStreamAddress(trigger.TenantId.ToString(), "tenant-invitations",
                Uuid.CreateVersion4().ToString()), trigger, 0, EventCursor.Start);
        public ClaimsPrincipal Actor { get; } =
            RequestActor.CreateSystem("reactor:TenantInvitationDeliveryV1", "bdgrz.system");
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid CauseId { get; } = Uuid.CreateVersion4();
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    }
}
