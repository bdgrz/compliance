using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests;

public sealed class UserIdentityTests
{
    [Fact]
    public void ShouldRecordOnlyProviderIdentityGivenRegistrationWithEmail()
    {
        // Arrange
        var id = Uuid.Parse("0e4149a5-a331-5e8b-a6cd-a6b904f61431", CultureInfo.InvariantCulture);
        var identity = new UserIdentity(id);

        // Act
        var result = identity.Register(id, "example-provider", "subject-42", "person@example.com");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(identity.IsRegistered);
        Assert.Equal(id, identity.UserId);
        Assert.Equal("example-provider", identity.Provider);
        Assert.Equal("subject-42", identity.Identifier);
        Assert.Equal("person@example.com", identity.EmailAddress);
        Assert.DoesNotContain(
            typeof(UserIdentity).GetProperties(),
            property => property.Name.Contains("Verified", StringComparison.Ordinal));
    }

    [Fact]
    public void ShouldAllowMissingEmailGivenProviderDoesNotSupplyOne()
    {
        // Arrange
        var id = Uuid.Parse("e38153af-149e-5ce1-a59e-a2bd93613c28", CultureInfo.InvariantCulture);
        var identity = new UserIdentity(id);

        // Act
        var result = identity.Register(id, "example-provider", "subject-43", emailAddress: null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(identity.IsRegistered);
        Assert.Null(identity.EmailAddress);
    }

    [Fact]
    public void ShouldReturnConflictGivenRepeatedRegistration()
    {
        // Arrange
        var id = Uuid.Parse("a72f3de7-c50a-55df-a716-2c3120239675", CultureInfo.InvariantCulture);
        var identity = new UserIdentity(id);
        _ = identity.Register(id, "example-provider", "subject-44", "person@example.com");

        // Act
        var result = identity.Register(id, "example-provider", "subject-44", "person@example.com");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
    }
}
