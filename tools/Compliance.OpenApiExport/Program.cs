using System.Runtime.CompilerServices;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance;

/// <summary>Exports Compliance's OpenAPI document to a file, without a live Fitz broker.</summary>
static class ExportTool
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: Compliance.OpenApiExport <output-path>");
            return 1;
        }

        var outputPath = args[0];

        // Boots the real Compliance.App composition in-process, the same way the app's own web
        // tests do (see UserIdentityContinuationWebTests.CreateFactory), so the exported document
        // always reflects the endpoints Portia actually registers rather than a hand-maintained
        // snapshot. Hosted services are stripped because they connect to a real Fitz broker, which
        // this export does not need or want as a build-time dependency.
        await using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            // WebApplicationFactory otherwise resolves the content root relative to the current
            // working directory, which is wrong whenever this tool runs from outside its own
            // project directory (e.g. `dotnet run --project tools/Compliance.OpenApiExport` from
            // the repo root).
            builder.UseContentRoot(ComplianceAppContentRoot());
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "compliance-openapi-export");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IEventStore>();
                services.AddSingleton<IEventStore, InMemoryEventStore>();
            });
        });
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadAsStringAsync();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        await File.WriteAllTextAsync(outputPath, document);
        Console.WriteLine($"Wrote OpenAPI document to {outputPath}.");
        return 0;
    }

    static string ComplianceAppContentRoot([CallerFilePath] string sourceFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", "..", "src", "Compliance.App"));
}
