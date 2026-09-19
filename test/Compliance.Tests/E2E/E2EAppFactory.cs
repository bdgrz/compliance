using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Builds a <see cref="WebApplicationFactory{TEntryPoint}" /> that connects for real to the
///     e2e broker stack -- unlike <c>ComplianceWebTests.CreateBrokerFreeFactory</c>, nothing here
///     is stripped, so writes really go through Fitz KV and real JSON serialization, and scans
///     really go over the wire. That's the point: this is what makes the suite able to catch bugs
///     <c>Cntryl.Fitz.Testing.InMemoryKvClient</c>-backed unit tests structurally cannot.
/// </summary>
static class E2EAppFactory
{
    public static WebApplicationFactory<Program> Create(BrokerStackFixture broker) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", broker.WebSocketEndpoint);
            builder.UseSetting("Fitz:ApplicationName", $"compliance-e2e-{Guid.NewGuid():N}");
            builder.UseSetting("Fitz:StartupTimeoutSeconds", "30");
        });
}
