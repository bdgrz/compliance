using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.Controls;

[Trait("Category", "WebIntegration")]
public sealed class ControlOwnerPersonReadContractTests
{
    [Fact]
    public async Task ShouldReturnSavedOwnerAndExplicitNullGivenAuthorizedHttpGet()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(tenantId, programId, "OWNER-HTTP");
        var personId = Uuid.CreateVersion4();
        var designationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(actorId, "Saved author");
        var at = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        await ProgramManagementServices.SeedAsync(factory.Services,
            new ControlDraft(tenantId, controlId), control =>
            {
                var created = control.Create(programId, Uuid.CreateVersion4(), "OWNER-HTTP",
                    new ControlDraftContent("Access review", "Review access", "Description",
                        "Review quarterly.", ["Review record"]), actorId, actor.Display, at);
                Assert.True(created.IsSuccess);
                Assert.Null(control.DesignatePersonOwner(programId, 1, designationId, personId,
                    Uuid.CreateVersion4(), "Saved rationale.", actorId, actor, at));
                return Result.Success;
            });
        var path = $"/api/v1/tenants/{tenantId}/programs/{programId}/controls/{controlId}/draft/owner-person";
        using var anonymous = await client.GetAsync(path, CancellationToken.None);
        using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions",
            new Login("owner-reader@example.com"), CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // Act
        using var response = await client.GetAsync(path, CancellationToken.None);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            CancellationToken.None));
        await ProgramManagementServices.SeedAsync(factory.Services,
            new ControlDraft(tenantId, controlId), control =>
            {
                Assert.Null(control.DesignatePersonOwner(programId, 1, Uuid.CreateVersion4(),
                    null, null, "Cleared.", actorId, actor, at.AddMinutes(1)));
                return Result.Success;
            });
        using var cleared = await client.GetAsync(path, CancellationToken.None);
        using var clearedDocument = JsonDocument.Parse(await cleared.Content.ReadAsStringAsync(
            CancellationToken.None));
        await ProgramManagementServices.SeedAsync(factory.Services,
            new ControlDraft(tenantId, controlId), control =>
            {
                Assert.Null(control.DesignatePersonOwner(programId, 1, Uuid.CreateVersion4(),
                    personId, null, "Owner restored.", actorId, actor, at.AddMinutes(2)));
                var reviewId = Uuid.CreateVersion4();
                Assert.Null(control.Review(programId, 1, reviewId, "accept", "Reviewed.",
                    Uuid.CreateVersion4(), "Reviewer", at.AddMinutes(3)));
                Assert.Null(control.Approve(programId, 1, Uuid.CreateVersion4(), reviewId,
                    new DateOnly(2026, 10, 12), "Approved.", new HashSet<Uuid>(),
                    Uuid.CreateVersion4(), "Approver", at.AddMinutes(4),
                    verifiedPersonOwner: new ControlPersonOwner(personId, null)));
                return Result.Success;
            });
        using var closed = await client.GetAsync(path, CancellationToken.None);
        using var closedDocument = JsonDocument.Parse(await closed.Content.ReadAsStringAsync(
            CancellationToken.None));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var view = document.RootElement;
        Assert.Equal(["tenant_id", "program_id", "control_id", "draft_version_id", "revision",
            "designation"], view.EnumerateObject().Select(property => property.Name));
        Assert.Equal(tenantId.ToString(), view.GetProperty("tenant_id").GetString());
        Assert.Equal(programId.ToString(), view.GetProperty("program_id").GetString());
        Assert.Equal(controlId.ToString(), view.GetProperty("control_id").GetString());
        Assert.Equal(ControlVersionIds.Initial(controlId).ToString(),
            view.GetProperty("draft_version_id").GetString());
        Assert.Equal(1, view.GetProperty("revision").GetInt64());
        var designation = view.GetProperty("designation");
        Assert.Equal(["person_id", "designation_id", "rationale", "actor", "designated_at"],
            designation.EnumerateObject().Select(property => property.Name));
        Assert.Equal(personId.ToString(), designation.GetProperty("person_id").GetString());
        Assert.Equal(designationId.ToString(), designation.GetProperty("designation_id").GetString());
        Assert.Equal("Saved rationale.", designation.GetProperty("rationale").GetString());
        Assert.Equal(at, designation.GetProperty("designated_at").GetDateTimeOffset());
        Assert.Equal(actor, designation.GetProperty("actor").Deserialize(
            ComplianceCoreJsonContext.Default.ActorReference));
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Equal(JsonValueKind.Null, clearedDocument.RootElement.GetProperty("designation").ValueKind);
        Assert.Equal(1, clearedDocument.RootElement.GetProperty("revision").GetInt64());
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        Assert.Equal(JsonValueKind.Null, closedDocument.RootElement.GetProperty("draft_version_id").ValueKind);
        Assert.Equal(JsonValueKind.Null, closedDocument.RootElement.GetProperty("designation").ValueKind);
        Assert.Equal(1, closedDocument.RootElement.GetProperty("revision").GetInt64());
    }

    [Fact]
    public async Task ShouldDescribePrivateCorrelationFreeOwnerReadGivenOpenApi()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/openapi/v1.json", CancellationToken.None);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            CancellationToken.None));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var path = document.RootElement.GetProperty("paths").GetProperty(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/owner-person");
        Assert.True(path.TryGetProperty("put", out _));
        var operation = path.GetProperty("get");
        Assert.Equal(["tenant_id", "program_id", "control_id"], operation.GetProperty("parameters")
            .EnumerateArray().Select(parameter => parameter.GetProperty("name").GetString()));
        Assert.True(operation.TryGetProperty("security", out _));
        var schema = operation.GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var owner = schemas.GetProperty(schema.GetProperty("$ref").GetString()!.Split('/')[^1]);
        var properties = owner.GetProperty("properties");
        Assert.Equal(["tenant_id", "program_id", "control_id", "draft_version_id", "revision",
            "designation"], properties.EnumerateObject().Select(property => property.Name));
        var designation = properties.GetProperty("designation");
        if (designation.TryGetProperty("$ref", out var reference))
            designation = schemas.GetProperty(reference.GetString()!.Split('/')[^1]);
        Assert.Equal(["person_id", "designation_id", "rationale", "actor", "designated_at"],
            designation.GetProperty("properties").EnumerateObject().Select(property => property.Name));
        Assert.DoesNotContain("correlated", owner.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correlated", designation.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "control-owner-read-tests");
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(descriptor =>
                             descriptor.ServiceType == typeof(IHostedService) &&
                             descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle").ToArray())
                    services.Remove(descriptor);
                var store = new InMemoryEventStore();
                services.RemoveAll<IEventStore>();
                services.RemoveAll<IDomainEventReader>();
                services.AddSingleton<IEventStore>(store);
                services.AddSingleton<IDomainEventReader>(store);
                services.RemoveAll<IKvClient>();
                services.AddSingleton<IKvClient>(new InMemoryKvClient());
                services.RemoveAll<ITenantMembershipDirectoryReader>();
                services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true));
                services.RemoveAll<ITenantActivity>();
                services.AddSingleton<ITenantActivity, ActiveTenant>();
                services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                services.AddSingleton<IAccessGrantPermissionAuthorizer>(
                    new PermissionBackedAccessGrantPermissionAuthorizer(new RecordingPermissionAuthorizer(true)));
                services.RemoveAll<IProgramResourceScopeResolver>();
                services.AddSingleton(ProgramManagementServices.ResourceScopes());
            });
        });

    sealed record Login([property: JsonPropertyName("email_address")] string EmailAddress);
}
