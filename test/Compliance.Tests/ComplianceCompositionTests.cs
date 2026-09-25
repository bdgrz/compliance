using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests;

public sealed class ComplianceCompositionTests
{
    [Fact]
    public void ShouldReturnPortiaBuilderGivenSharedApplicationComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            })
            .Build();

        // Act
        var application = services.AddCompliance(configuration);

        // Assert
        Assert.NotNull(application);
    }

    [Fact]
    public async Task ShouldRejectUnprotectedRequestGivenSharedApplicationCompositionWhenPortiaValidationStarts()
    {
        // Arrange
        var builder = Compose();
        _ = builder.Services.AddPortia()
            .AddRequestHandler<UnprotectedCompositionRequestHandler>();
        RemoveFitzConnectionHostedService(builder.Services);
        using var host = builder.Build();

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        // Assert
        Assert.Contains(nameof(UnprotectedCompositionRequest), failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRejectUnprotectedRequestGivenSharedApplicationCompositionWhenPortiaDispatches()
    {
        // Arrange
        var builder = Compose();
        _ = builder.Services.AddPortia()
            .AddRequestHandler<UnprotectedCompositionRequestHandler>();
        await using var provider = builder.Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await bus.SendAsync(new UnprotectedCompositionRequest(), RequestActor.Anonymous));

        // Assert
        Assert.Contains(nameof(UnprotectedCompositionRequest), failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldStartGivenEveryRegisteredRequestHasAuthorizationWhenPortiaValidationStarts()
    {
        // Arrange
        var builder = Compose();
        RemoveFitzConnectionHostedService(builder.Services);
        using var host = builder.Build();

        // Act
        await host.StartAsync();

        // Assert
        await host.StopAsync();
    }

    static HostApplicationBuilder Compose()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        });
        _ = builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true);
        return builder;
    }

    static void RemoveFitzConnectionHostedService(IServiceCollection services)
    {
        var descriptor = services.Single(candidate =>
            candidate.ServiceType == typeof(IHostedService) && candidate.ImplementationFactory is not null);
        _ = services.Remove(descriptor);
    }
}
