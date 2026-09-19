using Bdgrz.Compliance;
using Microsoft.Extensions.Configuration;

namespace Bdgrz.Compliance.Tests.Features.Authentication;

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
    public void ShouldResolveSettingsGivenExternalProviderConfiguration()
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
        Assert.Equal("compliance-spa", settings.ClientId);
        Assert.Equal(["openid", "profile", "compliance.read"], settings.Scopes);
        Assert.Equal("https://api.example", settings.AuthorizationAudience);
        var resource = Assert.Single(settings.Resources);
        Assert.Equal("Default", resource.Name);
        Assert.Equal("https://issuer.example/", resource.Authority);
        Assert.Equal("compliance-api", resource.Audience);
        Assert.True(resource.RequireHttpsMetadata);
    }

    [Fact]
    public void ShouldResolveAuthoritiesGivenMultipleResources()
    {
        // Arrange
        var values = ValidExternalValues();
        values.Remove("Compliance:Authentication:Audience");
        values["Compliance:Authentication:Resources:Badgers:Audience"] = "badgers-api";
        values["Compliance:Authentication:Resources:Evidence:Authority"] = "https://evidence.example/";
        values["Compliance:Authentication:Resources:Evidence:Audience"] = "evidence-api";
        var configuration = BuildConfiguration(values);

        // Act
        var settings = ComplianceAuthenticationSettings.Resolve(configuration, isDevelopment: false);

        // Assert
        Assert.NotNull(settings);
        Assert.Collection(
            settings.Resources,
            resource =>
            {
                Assert.Equal("Badgers", resource.Name);
                Assert.Equal("https://issuer.example/", resource.Authority);
                Assert.Equal("badgers-api", resource.Audience);
            },
            resource =>
            {
                Assert.Equal("Evidence", resource.Name);
                Assert.Equal("https://evidence.example/", resource.Authority);
                Assert.Equal("evidence-api", resource.Audience);
            });
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
    public void ShouldAllowDeveloperModeGivenDevelopmentEnvironment()
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
    public void ShouldRejectDeveloperModeGivenProductionEnvironment()
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
    public void ShouldRejectDeveloperFlagGivenProductionEnvironment()
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
