using Bdgrz.Compliance;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Tests;

public sealed class ComplianceAuthenticationSettingsTests
{
    [Fact]
    public void ShouldFailClosedGivenMissingExternalConfiguration()
    {
        // Arrange
        var configuration = BuildConfiguration();

        // Act
        Action action = () => _ = ComplianceAuthenticationSettings.Resolve(
            configuration,
            isDevelopment: false);

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Theory]
    [InlineData("Authority")]
    [InlineData("Audience")]
    [InlineData("ClientId")]
    public void ShouldFailClosedGivenIncompleteExternalConfiguration(string missingKey)
    {
        // Arrange
        var values = ValidExternalValues();
        values.Remove($"Compliance:Authentication:{missingKey}");
        var configuration = BuildConfiguration(values);

        // Act
        Action action = () => _ = ComplianceAuthenticationSettings.Resolve(
            configuration,
            isDevelopment: false);

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void ShouldResolveProviderNeutralExternalSettings()
    {
        // Arrange
        var configuration = BuildConfiguration(ValidExternalValues());

        // Act
        var settings = ComplianceAuthenticationSettings.Resolve(
            configuration,
            isDevelopment: false);

        // Assert
        Assert.NotNull(settings);
        Assert.Equal("https://issuer.example/", settings.Authority);
        Assert.Equal("compliance-api", settings.Audience);
        Assert.Equal("compliance-spa", settings.ClientId);
        Assert.Equal(["openid", "profile", "compliance.read"], settings.Scopes);
        Assert.Equal("https://api.example", settings.AuthorizationAudience);
        Assert.True(settings.RequireHttpsMetadata);
    }

    [Fact]
    public void ShouldRejectHttpAuthorityGivenHttpsMetadataIsRequired()
    {
        // Arrange
        var values = ValidExternalValues();
        values["Compliance:Authentication:Authority"] = "http://issuer.example";
        var configuration = BuildConfiguration(values);

        // Act
        Action action = () => _ = ComplianceAuthenticationSettings.Resolve(
            configuration,
            isDevelopment: false);

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void ShouldAllowExplicitDevelopmentModeInDevelopment()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Compliance:Authentication:Mode"] = "Development",
        });

        // Act
        var settings = ComplianceAuthenticationSettings.Resolve(
            configuration,
            isDevelopment: true);

        // Assert
        Assert.Null(settings);
    }

    [Fact]
    public void ShouldRejectDevelopmentModeOutsideDevelopment()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Compliance:Authentication:Mode"] = "Development",
        });

        // Act
        Action action = () => _ = ComplianceAuthenticationSettings.Resolve(
            configuration,
            isDevelopment: false);

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void ShouldEnableDevelopmentModeGivenExplicitEnvironmentFlag()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BDGRZ_DEVELOPER_AUTH"] = "true",
        });

        // Act
        var settings = ComplianceAuthenticationSettings.Resolve(configuration, isDevelopment: true);

        // Assert
        Assert.Null(settings);
    }

    [Fact]
    public void ShouldRejectDevelopmentFlagOutsideDevelopment()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["BDGRZ_DEVELOPER_AUTH"] = "true",
        });

        // Act
        Action action = () => _ = ComplianceAuthenticationSettings.Resolve(
            configuration,
            isDevelopment: false);

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    static Dictionary<string, string?> ValidExternalValues() => new()
    {
        ["Compliance:Authentication:Mode"] = "External",
        ["Compliance:Authentication:Authority"] = "https://issuer.example/",
        ["Compliance:Authentication:Audience"] = "compliance-api",
        ["Compliance:Authentication:ClientId"] = "compliance-spa",
        ["Compliance:Authentication:Scopes"] = "openid profile compliance.read",
        ["Compliance:Authentication:AuthorizationAudience"] = "https://api.example",
        ["Compliance:Authentication:RequireHttpsMetadata"] = "true",
    };

    static IConfiguration BuildConfiguration(
        IDictionary<string, string?>? values = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
