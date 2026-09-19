using System.Globalization;
using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class UserIdentityTests
{
    [Fact]
    public void ShouldRecordOnlyProviderIdentityGivenRegistrationWithEmail()
    {
        // Arrange
        var userId = Uuid.Parse("0e4149a5-a331-5e8b-a6cd-a6b904f61431", CultureInfo.InvariantCulture);
        var identity = new UserIdentity("example-provider", "subject-42");

        // Act
        var result = identity.Register(userId, "person@example.com");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(identity.Id, result.Value.UserIdentityId);
        Assert.Equal(userId, result.Value.UserId);
        Assert.Equal("person@example.com", result.Value.EmailAddress);
        var property = Assert.Single(typeof(UserIdentity).GetProperties(), property =>
            property.DeclaringType == typeof(UserIdentity));
        Assert.Equal(nameof(UserIdentity.IsRegistered), property.Name);
        Assert.True(identity.IsRegistered);
    }

    [Fact]
    public void ShouldAllowMissingEmailGivenProviderDoesNotSupplyOne()
    {
        // Arrange
        var userId = Uuid.Parse("e38153af-149e-5ce1-a59e-a2bd93613c28", CultureInfo.InvariantCulture);
        var identity = new UserIdentity("example-provider", "subject-43");

        // Act
        var result = identity.Register(userId, emailAddress: null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.EmailAddress);
    }

    [Fact]
    public void ShouldReturnExistingIdentityGivenRepeatedRegistration()
    {
        // Arrange
        var userId = Uuid.Parse("a72f3de7-c50a-55df-a716-2c3120239675", CultureInfo.InvariantCulture);
        var identity = new UserIdentity("example-provider", "subject-44");
        _ = identity.Register(userId, "person@example.com");

        // Act
        var result = identity.Register(userId, "person@example.com");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value.UserId);
    }

    [Fact]
    public void ShouldReturnConflictGivenIdentityBelongsToAnotherUser()
    {
        // Arrange
        var identity = new UserIdentity("example-provider", "subject-44");
        _ = identity.Register(
            Uuid.Parse("a72f3de7-c50a-55df-a716-2c3120239675", CultureInfo.InvariantCulture),
            "person@example.com");

        // Act
        var result = identity.Register(
            Uuid.Parse("43b27197-e14b-47f5-a09b-144526a53adc", CultureInfo.InvariantCulture),
            "person@example.com");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }

    [Fact]
    public void ShouldDeriveSameAggregateIdGivenSameProviderIdentity()
    {
        // Arrange
        const string issuer = "example-provider";

        // Act
        var first = new UserIdentity(issuer, "subject-42");
        var second = new UserIdentity(issuer, "subject-42");
        var another = new UserIdentity(issuer, "subject-43");

        // Assert
        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(first.Id, another.Id);
    }

    [Fact]
    public async Task ShouldAuditAuthenticationGivenRegisteredProviderIdentity()
    {
        // Arrange
        var userId = Uuid.Parse("5e87ff38-8dad-404f-a129-56a02ca2dd73", CultureInfo.InvariantCulture);
        var identity = new UserIdentity("example-provider", "subject-42");
        await using var fixture = new StoreFixture();
        _ = identity.Register(userId, "person@example.com");
        await fixture.Repository.SaveAsync(identity, new ExecutionContext(), CancellationToken.None);
        identity = await fixture.Repository.HydrateAsync(
            new UserIdentity("example-provider", "subject-42"),
            CancellationToken.None);
        var scenario = new AggregateScenario<UserIdentity>(identity);
        var version = identity.Version;

        // Act
        var result = identity.Authenticate();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(identity.Id, result.Value.UserIdentityId);
        Assert.Equal(userId, result.Value.UserId);
        Assert.Equal("person@example.com", result.Value.EmailAddress);
        Assert.Equal(version, identity.Version);
        var audit = Assert.Single(scenario.PendingAudits);
        Assert.Equal("UserIdentityAuthenticated", audit.GetType().Name);
        Assert.True(audit.Metadata.IsAudit);
        Assert.Equal(identity.Id, audit.Metadata.AggregateId);
    }

    [Fact]
    public void ShouldReturnNotFoundGivenUnregisteredProviderIdentity()
    {
        // Arrange
        var identity = new UserIdentity("example-provider", "unknown-subject");
        var scenario = new AggregateScenario<UserIdentity>(identity);

        // Act
        var result = identity.Authenticate();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error.Kind);
        Assert.Equal(0UL, identity.Version);
        Assert.Empty(scenario.PendingAudits);
    }

    sealed class ExecutionContext : IExecutionContext
    {
        public ClaimsPrincipal Actor { get; } = new(new ClaimsIdentity());
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid CauseId { get; } = Uuid.CreateVersion4();
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    }
}
