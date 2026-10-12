using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.Applications;

/// <summary>Exercises production HTTP and registered MCP binding with local event and permission observations.</summary>
public sealed class ApplicationRevisionFieldPresenceHttpTests
{
    [Theory]
    [InlineData(false, "classification")]
    [InlineData(false, "system_owner_person_id")]
    [InlineData(false, "access_owner_person_id")]
    [InlineData(true, "classification")]
    [InlineData(true, "system_owner_person_id")]
    [InlineData(true, "access_owner_person_id")]
    public async Task ShouldRetainOmissionAndRejectExplicitNullWithoutAppendGivenGovernedMcpTool(
        bool preview, string field)
    {
        // Arrange
        await using var fixture = new Fixture(true);
        await fixture.SeedAsync();
        await using var scenario = await fixture.ConnectMcpAsync();
        var toolName = preview ? "bdgrz.application.change.preview" : "bdgrz.application.revise";
        var before = await fixture.EventsAsync();
        var arguments = fixture.McpArguments(preview, 1);

        // Act
        await scenario.When(toolName, arguments).ExpectSuccess();
        var retained = await fixture.EventsAsync();
        arguments[preview ? "expected_application_revision" : "expected_revision"] = preview ? 1 : 2;
        arguments[field] = null;
        var cleared = await scenario.When(toolName, arguments);

        // Assert
        if (preview)
            Assert.Equal(before, retained);
        else
        {
            Assert.Equal(before.Length + 1, retained.Length);
            var revised = Assert.IsType<ApplicationRevised>(retained[^1].Event);
            Assert.Equal("internal", revised.Classification);
            Assert.Equal(fixture.Owner, revised.SystemOwnerPersonId);
            Assert.Equal(fixture.Owner, revised.AccessOwnerPersonId);
        }
        Assert.True(cleared.IsError, "An explicit required-field clear must fail validation.");
        Assert.Equal("Validation", cleared.Error?.GetProperty("kind").GetString());
        Assert.Equal(retained, await fixture.EventsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveAdvertisedSchemaAndHintsGivenRegisteredMcpTool(bool preview)
    {
        // Arrange: the input schema is frozen from the pre-adapter registered tools.
        await using var fixture = new Fixture(false);
        await fixture.SeedAsync();
        await using var scenario = await fixture.ConnectMcpAsync();
        var expected = JsonNode.Parse("""
            {"type":"object","properties":{
              "tenant_id":true,"application_id":true,
              "expected_revision":{"type":["string","integer"],"pattern":"^-?(?:0|[1-9]\\d*)$"},
              "name":{"type":"string"},"purpose":{"type":"string"},
              "owner_reference":{"type":["string","null"]},
              "classification":{"type":["string","null"],"default":null},
              "system_owner_person_id":{"default":null},"access_owner_person_id":{"default":null},
              "is_restricted":{"type":["boolean","null"],"default":null},
              "application_kind":{"type":["string","null"],"default":null},
              "criticality":{"type":["string","null"],"default":null}},
             "required":["tenant_id","application_id","expected_revision","name","purpose","owner_reference"]}
            """)!;
        if (preview)
        {
            expected["required"] = new JsonArray("tenant_id", "application_id", "expected_application_revision", "change_kind");
            var properties = expected["properties"]!.AsObject();
            properties["expected_application_revision"] = properties["expected_revision"]!.DeepClone();
            properties.Remove("expected_revision");
            properties.Remove("is_restricted");
            properties["change_kind"] = JsonNode.Parse("""{"type":"string"}""");
            foreach (var field in new[] { "name", "purpose", "owner_reference" })
                properties[field] = JsonNode.Parse("""{"type":["string","null"],"default":null}""");
        }

        // Act
        var tools = await scenario.ListTools();
        var tool = Assert.Single(tools, item => item.Name == McpToolName(preview));

        // Assert: default:null is a constructor annotation; setters still default to null.
        Assert.True(JsonNode.DeepEquals(WithoutNullDefaults(expected),
            WithoutNullDefaults(JsonNode.Parse(tool.InputSchema.GetRawText())!)), tool.InputSchema.GetRawText());
        Assert.Equal(preview ? "Invokes the PreviewApplicationChange request." : "Invokes the ReviseApplication request.", tool.Description);
        Assert.Null(tool.Title);
        Assert.Equal(preview ? true : (bool?)null, tool.ReadOnly);
        Assert.Equal(preview ? false : (bool?)null, tool.Destructive);
        Assert.Equal(preview ? null : (bool?)true, tool.Idempotent);
        Assert.Null(tool.OpenWorld);
        Assert.Null(tool.Metadata);
        Assert.Equal(preview, tool.OutputSchema.HasValue);
    }

    [Theory]
    [InlineData(false, "classification")]
    [InlineData(false, "system_owner_person_id")]
    [InlineData(false, "access_owner_person_id")]
    [InlineData(true, "classification")]
    [InlineData(true, "system_owner_person_id")]
    [InlineData(true, "access_owner_person_id")]
    public async Task ShouldPreserveLegacyClearGivenUngovernedMcpTool(bool preview, string field)
    {
        // Arrange
        await using var fixture = new Fixture(false);
        await fixture.SeedAsync();
        await using var scenario = await fixture.ConnectMcpAsync();
        var before = await fixture.EventsAsync();
        var arguments = fixture.McpArguments(preview, 1);
        arguments[field] = null;

        // Act
        var result = await scenario.When(McpToolName(preview), arguments).ExpectSuccess();

        // Assert
        var after = await fixture.EventsAsync();
        if (preview)
        {
            Assert.Equal(before, after);
            Assert.Contains(result.StructuredJson!.Value.GetProperty("result").GetProperty("changes").EnumerateArray(),
                change => change.GetProperty("field").GetString() == field && change.GetProperty("after").ValueKind == JsonValueKind.Null);
        }
        else
        {
            Assert.Equal(before.Length + 1, after.Length);
            var revised = Assert.IsType<ApplicationRevised>(after[^1].Event);
            Assert.Null(revised.Classification);
            Assert.Null(revised.SystemOwnerPersonId);
            Assert.Null(revised.AccessOwnerPersonId);
        }
    }

    [Theory]
    [InlineData(false, "tenant", "NotFound")]
    [InlineData(true, "tenant", "NotFound")]
    [InlineData(false, "owner", "Validation")]
    [InlineData(true, "owner", "Validation")]
    [InlineData(false, "stale", "Conflict")]
    [InlineData(true, "stale", "Conflict")]
    public async Task ShouldRetainExistingFenceWithoutAppendGivenInvalidMcpToolRequest(
        bool preview, string invalid, string kind)
    {
        // Arrange
        await using var fixture = new Fixture(true);
        await fixture.SeedAsync();
        await using var scenario = await fixture.ConnectMcpAsync();
        var before = await fixture.EventsAsync();
        var arguments = fixture.McpArguments(preview, invalid == "stale" ? 2 : 1);
        if (invalid == "tenant")
            arguments["tenant_id"] = Uuid.CreateVersion4().ToString();
        if (invalid == "owner")
            arguments["system_owner_person_id"] = Uuid.CreateVersion4().ToString();

        // Act
        await scenario.When(McpToolName(preview), arguments).ExpectFailure(kind);

        // Assert
        Assert.Equal(before, await fixture.EventsAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldRejectUnauthorizedActorWithoutAppendGivenMcpTool(
        bool preview, bool inventoryAllowed)
    {
        // Arrange
        await using var fixture = new Fixture(true, inventoryAllowed, previewAllowed: false);
        await fixture.SeedAsync();
        await using var scenario = await fixture.ConnectMcpAsync();
        var before = await fixture.EventsAsync();

        // Act
        await scenario.When(McpToolName(preview), fixture.McpArguments(preview, 1)).ExpectFailure("Forbidden");

        // Assert
        Assert.Equal(before, await fixture.EventsAsync());
        if (inventoryAllowed)
            Assert.Contains(fixture.Calls, call => call.Request is PreviewApplicationChange);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldForwardOriginalActorAndExecutionContextGivenMcpTool(bool preview)
    {
        // Arrange
        await using var fixture = new Fixture(true);
        await fixture.SeedAsync();
        await using var scenario = await fixture.ConnectMcpAsync();
        fixture.Calls.Clear();

        // Act
        await scenario.When(McpToolName(preview), fixture.McpArguments(preview, 1)).ExpectSuccess();

        // Assert
        var outer = Assert.Single(fixture.Calls, call => preview
            ? call.Request is PreviewApplicationChangeMcpInput : call.Request is ReviseApplicationMcpInput);
        var inner = Assert.Single(fixture.Calls, call => preview
            ? call.Request is PreviewApplicationChange : call.Request is ReviseApplication);
        Assert.Equal(outer.Context.ExecutionId, inner.Context.ExecutionId);
        Assert.Equal(outer.Context.StartedAt, inner.Context.StartedAt);
        Assert.Same(outer.Context.Metadata, inner.Context.Metadata);
        Assert.Same(outer.Context.Invocation, inner.Context.Invocation);
        Assert.IsType<McpInvocation>(inner.Context.Invocation);
        Assert.True(inner.Context.Actor.Identity!.IsAuthenticated);
        Assert.Equal(outer.Context.Actor.Claims.Select(claim => (claim.Type, claim.Value)),
            inner.Context.Actor.Claims.Select(claim => (claim.Type, claim.Value)));
    }

    static string McpToolName(bool preview) => preview ? "bdgrz.application.change.preview" : "bdgrz.application.revise";

    static JsonNode WithoutNullDefaults(JsonNode node)
    {
        if (node is JsonObject value)
        {
            if (value.ContainsKey("default") && value["default"] is null)
                value.Remove("default");
            foreach (var property in value.ToArray())
                if (property.Value is not null)
                    value[property.Key] = WithoutNullDefaults(property.Value.DeepClone());
            if (value.Count == 0)
                return JsonValue.Create(true)!;
        }
        else if (node is JsonArray array)
            for (var index = 0; index < array.Count; index++)
                if (array[index] is { } item)
                    array[index] = WithoutNullDefaults(item.DeepClone());
        return node;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldCancelBodyReadWithoutAppendGivenAbortedProductionHttpRequest(bool preview)
    {
        // Arrange
        await using var fixture = new Fixture(true);
        await fixture.SeedAsync();
        var before = await fixture.EventsAsync();
        using var cancellation = new CancellationTokenSource();
        using var body = new BlockingReadStream();

        // Act
        var request = fixture.AbortableRequestAsync(preview, body, cancellation.Token);
        await body.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await request);

        // Assert
        Assert.True(body.CancellationObserved);
        Assert.Equal(before, await fixture.EventsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetainScalarQueryFallbackGivenOmittedHttpBodyMembers(bool preview)
    {
        // Arrange
        await using var fixture = new Fixture(true);
        await fixture.SeedAsync();
        using var body = new StringContent("{}", Encoding.UTF8, "application/json");
        var query = $"?{(preview ? "expected_application_revision" : "expected_revision")}=1&change_kind=revise&name=Payroll&purpose=Run%20payroll&owner_reference=Finance&classification=internal&system_owner_person_id={fixture.Owner}&access_owner_person_id={fixture.Owner}&is_restricted=true";

        // Act
        using var response = await fixture.SendAsync(preview, body, query);

        // Assert
        Assert.Equal(preview ? HttpStatusCode.OK : HttpStatusCode.NoContent, response.StatusCode);
        var events = await fixture.EventsAsync();
        if (preview)
            Assert.Single(events);
        else
        {
            var revised = Assert.IsType<ApplicationRevised>(events[^1].Event);
            Assert.Equal("internal", revised.Classification);
            Assert.Equal(fixture.Owner, revised.SystemOwnerPersonId);
            Assert.Equal(fixture.Owner, revised.AccessOwnerPersonId);
            Assert.True(revised.IsRestricted);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectExplicitBodyClearWithoutAppendGivenConflictingQueryValue(bool preview)
    {
        // Arrange
        await using var fixture = new Fixture(true);
        await fixture.SeedAsync();
        var before = await fixture.EventsAsync();
        using var body = new StringContent($"{{\"{(preview ? "expected_application_revision" : "expected_revision")}\":1,\"change_kind\":\"revise\",\"name\":\"Payroll\",\"purpose\":\"Run payroll\",\"classification\":null}}", Encoding.UTF8, "application/json");

        // Act
        using var response = await fixture.SendAsync(preview, body, "?classification=internal");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await fixture.EventsAsync());
    }

    [Theory]
    [InlineData(false, "classification", "null")]
    [InlineData(false, "system_owner_person_id", "null")]
    [InlineData(false, "access_owner_person_id", "null")]
    [InlineData(false, "classification", "\"  \\t  \"")]
    [InlineData(true, "classification", "null")]
    [InlineData(true, "system_owner_person_id", "null")]
    [InlineData(true, "access_owner_person_id", "null")]
    [InlineData(true, "classification", "\"  \\t  \"")]
    [InlineData(true, "system_owner_person_id", "\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData(true, "access_owner_person_id", "\"00000000-0000-0000-0000-000000000000\"")]
    public async Task ShouldMatchPreviewAndSaveWithoutUnintendedAppendGivenExplicitHttpIntent(
        bool enabled, string field, string value)
    {
        // Arrange
        await using var fixture = new Fixture(enabled);
        await fixture.SeedAsync();
        var before = await fixture.EventsAsync();
        var fields = $",\"{field}\":{value}";

        // Act
        using var preview = await fixture.PreviewAsync(1, fields);
        var afterPreview = await fixture.EventsAsync();
        using var saved = await fixture.ReviseAsync(1, fields);

        // Assert
        Assert.Equal(before, afterPreview);
        if (enabled)
        {
            Assert.Equal(HttpStatusCode.BadRequest, preview.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, saved.StatusCode);
            Assert.Equal(before, await fixture.EventsAsync());
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
            using var json = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
            Assert.Contains(json.RootElement.GetProperty("changes").EnumerateArray(),
                change => change.GetProperty("field").GetString() == field &&
                    change.GetProperty("after").ValueKind == JsonValueKind.Null);
            var after = await fixture.EventsAsync();
            Assert.Equal(before.Length + 1, after.Length);
            var revised = Assert.IsType<ApplicationRevised>(after[^1].Event);
            Assert.Null(revised.Classification);
            Assert.Null(revised.SystemOwnerPersonId);
            Assert.Null(revised.AccessOwnerPersonId);
        }
    }

    [Fact]
    public async Task ShouldRetainRequiredFieldsInBothPathsGivenOmittedGovernedHttpFields()
    {
        // Arrange
        await using var fixture = new Fixture(true);
        await fixture.SeedAsync();
        var before = await fixture.EventsAsync();

        // Act
        using var preview = await fixture.PreviewAsync(1);
        using var saved = await fixture.ReviseAsync(1);

        // Assert
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var json = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.GetProperty("changes").EnumerateArray());
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        var after = await fixture.EventsAsync();
        Assert.Equal(before.Length + 1, after.Length);
        var revised = Assert.IsType<ApplicationRevised>(after[^1].Event);
        Assert.Equal("internal", revised.Classification);
        Assert.Equal(fixture.Owner, revised.SystemOwnerPersonId);
        Assert.Equal(fixture.Owner, revised.AccessOwnerPersonId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveExactBodySchemaAndPathParametersGivenHostedOpenApi(bool preview)
    {
        // Arrange
        await using var fixture = new Fixture(false);
        var expected = JsonNode.Parse("""
            {"content":{"application/json":{"schema":{"required":["expected_revision","name","purpose"],"type":"object","properties":{
              "expected_revision":{"pattern":"^-?(?:0|[1-9]\\d*)$","type":["integer","string"]},
              "name":{"type":["null","string"]},"purpose":{"type":["null","string"]},
              "owner_reference":{"type":["null","string"]},"classification":{"type":["null","string"]},
              "system_owner_person_id":{"type":"string","format":"uuid"},"access_owner_person_id":{"type":"string","format":"uuid"},
              "is_restricted":{"type":"boolean"},"application_kind":{"type":["null","string"]},"criticality":{"type":["null","string"]}
            }}}},"required":true}
            """)!;
        if (preview)
        {
            var schema = expected["content"]!["application/json"]!["schema"]!;
            schema["required"] = new JsonArray("expected_application_revision", "change_kind");
            var properties = schema["properties"]!.AsObject();
            properties["expected_application_revision"] = properties["expected_revision"]!.DeepClone();
            properties.Remove("expected_revision");
            properties.Remove("is_restricted");
            properties["change_kind"] = JsonNode.Parse("""{"type":["null","string"]}""");
        }
        var parameters = JsonNode.Parse("""
            [{"name":"tenant_id","in":"path","required":true,"schema":{"type":"string","format":"uuid"}},
             {"name":"application_id","in":"path","required":true,"schema":{"type":"string","format":"uuid"}}]
            """);

        // Act
        var operation = await fixture.OpenApiOperationAsync(preview);

        // Assert
        Assert.True(JsonNode.DeepEquals(expected, operation["requestBody"]), operation["requestBody"]!.ToJsonString());
        Assert.True(JsonNode.DeepEquals(parameters, operation["parameters"]), operation["parameters"]!.ToJsonString());
    }

    [Theory]
    [InlineData(false, "malformed", HttpStatusCode.BadRequest)]
    [InlineData(true, "malformed", HttpStatusCode.BadRequest)]
    [InlineData(false, "array", HttpStatusCode.BadRequest)]
    [InlineData(true, "array", HttpStatusCode.BadRequest)]
    [InlineData(false, "empty", HttpStatusCode.BadRequest)]
    [InlineData(true, "empty", HttpStatusCode.BadRequest)]
    [InlineData(false, "media", HttpStatusCode.UnsupportedMediaType)]
    [InlineData(true, "media", HttpStatusCode.UnsupportedMediaType)]
    [InlineData(false, "untyped", HttpStatusCode.UnsupportedMediaType)]
    [InlineData(true, "untyped", HttpStatusCode.UnsupportedMediaType)]
    [InlineData(false, "size", HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(true, "size", HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(false, "size_streamed", HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(true, "size_streamed", HttpStatusCode.RequestEntityTooLarge)]
    public async Task ShouldPreserveIngressFailureWithoutAppendGivenInvalidHttpBody(
        bool preview, string invalid, HttpStatusCode expected)
    {
        // Arrange
        await using var fixture = new Fixture(true);
        await fixture.SeedAsync();
        var before = await fixture.EventsAsync();
        var json = invalid switch
        {
            "malformed" => "{broken",
            "array" => "[]",
            "empty" => "",
            "size" or "size_streamed" => "{\"padding\":\"" + new string('x', 1024) + "\"}",
            _ => "{}",
        };
        using var body = invalid == "size_streamed" ? new UnknownLengthContent(json) :
            new StringContent(json, Encoding.UTF8, invalid == "media" ? "text/plain" : "application/json");
        if (invalid == "untyped")
            body.Headers.ContentType = null;

        // Act
        using var response = await fixture.SendAsync(preview, body);

        // Assert
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(before, await fixture.EventsAsync());
    }

    [Theory]
    [InlineData("classification")]
    [InlineData("system_owner_person_id")]
    [InlineData("access_owner_person_id")]
    public async Task ShouldRetainOmissionAndRejectExplicitNullWithoutAppendGivenGovernedHttpRevision(string field)
    {
        // Arrange
        await using var fixture = new Fixture(true);
        await fixture.SeedAsync();

        // Act
        using var omitted = await fixture.ReviseAsync(1);
        var retained = await fixture.EventsAsync();
        using var cleared = await fixture.ReviseAsync(2, $",\"{field}\":null");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, omitted.StatusCode);
        Assert.Equal(2, retained.Length);
        var revised = Assert.IsType<ApplicationRevised>(retained[^1].Event);
        Assert.Equal("internal", revised.Classification);
        Assert.Equal(fixture.Owner, revised.SystemOwnerPersonId);
        Assert.Equal(fixture.Owner, revised.AccessOwnerPersonId);
        Assert.Equal(HttpStatusCode.BadRequest, cleared.StatusCode);
        Assert.Equal(retained, await fixture.EventsAsync());
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly WebApplicationFactory<Program> _factory;
        readonly HttpClient _client;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Application { get; } = Uuid.CreateVersion4();
        public Uuid Owner { get; } = Uuid.CreateVersion4();
        public List<BusCall> Calls { get; } = [];
        readonly InMemoryEventStore _events = new();
        string _cookie = "";

        public Fixture(bool enabled, bool inventoryAllowed = true, bool previewAllowed = true)
        {
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
                builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
                builder.UseSetting("Fitz:ApplicationName", "application-field-presence-tests");
                builder.UseSetting("Compliance:Applications:GovernedRevisionsEnabled", enabled.ToString());
                builder.ConfigureServices(services =>
                {
                    foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                                 descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle").ToArray())
                        services.Remove(descriptor);
                    services.RemoveAll<IEventStore>();
                    services.RemoveAll<IDomainEventReader>();
                    services.AddSingleton<IEventStore>(_events);
                    services.AddSingleton<IDomainEventReader>(_events);
                    services.RemoveAll<IKvClient>();
                    services.AddSingleton<IKvClient>(new InMemoryKvClient());
                    services.RemoveAll<ITenantMembershipDirectoryReader>();
                    services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true));
                    services.RemoveAll<ITenantActivity>();
                    services.AddSingleton<ITenantActivity, ActiveTenant>();
                    services.RemoveAll<IPermissionAuthorizer>();
                    services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(inventoryAllowed));
                    services.RemoveAll<IAccessGrantPermissionAuthorizer>();
                    services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PreviewPermissions(previewAllowed));
                    services.Configure<PortiaHttpOptions>(options => options.MaxJsonBodyBytes = 512);
                    var bus = services.Last(descriptor => descriptor.ServiceType == typeof(IRequestBus));
                    services.RemoveAll<IRequestBus>();
                    services.Add(new ServiceDescriptor(typeof(IRequestBus), provider =>
                        new ObservedBus((IRequestBus)(bus.ImplementationInstance ??
                            bus.ImplementationFactory?.Invoke(provider) ??
                            ActivatorUtilities.CreateInstance(provider, bus.ImplementationType!)), Calls), bus.Lifetime));
                });
            });
            _client = _factory.CreateClient();
        }

        public async Task SeedAsync()
        {
            using var loginBody = new StringContent("{\"email_address\":\"application-author@example.com\"}", Encoding.UTF8, "application/json");
            using var login = await _client.PostAsync("/api/v1/developer-user-sessions", loginBody);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            _cookie = string.Join("; ", login.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]));
            await using var scope = _factory.Services.CreateAsyncScope();
            var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            var actor = Uuid.CreateVersion4();
            var now = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
            var owner = new Person(Tenant, Owner);
            Assert.True(owner.Record("Owner", null, ActorReference.ForMember(actor, "Author"), now).IsSuccess);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            var application = new DeclaredApplication(Tenant, Application);
            Assert.True(application.Declare("Payroll", "Run payroll", "Finance", actor,
                "Author", now, "internal", Owner, Owner, applicationKind: "service", criticality: "critical").IsSuccess);
            await writer.SaveAsync(application, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            var directory = scope.ServiceProvider.GetRequiredService<FitzApplicationDirectory>();
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity("ApplicationDirectoryV2", EventStreamPattern.ForPattern(Tenant.ToString())),
                ProjectionCheckpoint.Start));
            foreach (var record in await EventsAsync())
                await directory.ApplyAsync(record.Event);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        public async Task<HttpResponseMessage> ReviseAsync(long revision, string fields = "")
        {
            using var body = new StringContent($"{{\"expected_revision\":{revision},\"name\":\"Payroll\",\"purpose\":\"Run payroll\",\"owner_reference\":\"Finance\"{fields}}}", Encoding.UTF8, "application/json");
            return await _client.PutAsync($"/api/v1/tenants/{Tenant}/applications/{Application}", body);
        }

        public ValueTask<McpScenario> ConnectMcpAsync() => McpScenario.ConnectAsync(_client, new Uri(_client.BaseAddress!, "/mcp"));

        public Dictionary<string, object?> McpArguments(bool preview, long revision)
        {
            var arguments = new Dictionary<string, object?>
            {
                ["tenant_id"] = Tenant.ToString(),
                ["application_id"] = Application.ToString(),
                [preview ? "expected_application_revision" : "expected_revision"] = revision,
                ["name"] = "Payroll",
                ["purpose"] = "Run payroll",
                ["owner_reference"] = "Finance",
            };
            if (preview)
                arguments["change_kind"] = "revise";
            return arguments;
        }

        public async Task<HttpResponseMessage> PreviewAsync(long revision, string fields = "")
        {
            using var body = new StringContent($"{{\"expected_application_revision\":{revision},\"change_kind\":\"revise\",\"name\":\"Payroll\",\"purpose\":\"Run payroll\",\"owner_reference\":\"Finance\"{fields}}}", Encoding.UTF8, "application/json");
            return await SendAsync(true, body);
        }

        public Task<HttpResponseMessage> SendAsync(bool preview, HttpContent body, string query = "") => preview
            ? _client.PostAsync($"/api/v1/tenants/{Tenant}/applications/{Application}/change-previews{query}", body)
            : _client.PutAsync($"/api/v1/tenants/{Tenant}/applications/{Application}{query}", body);

        public Task<HttpContext> AbortableRequestAsync(bool preview, Stream body, CancellationToken ct) =>
            _factory.Server.SendAsync(context =>
            {
                context.Request.Method = preview ? "POST" : "PUT";
                context.Request.Path = $"/api/v1/tenants/{Tenant}/applications/{Application}" + (preview ? "/change-previews" : "");
                context.Request.ContentType = "application/json";
                context.Request.Headers.Cookie = _cookie;
                context.Request.Body = body;
            }, ct);

        public async Task<JsonNode> OpenApiOperationAsync(bool preview)
        {
            var document = JsonNode.Parse(await _client.GetStringAsync("/openapi/v1.json"))!;
            var operation = document["paths"]!["/api/v1/tenants/{tenant_id}/applications/{application_id}" +
                (preview ? "/change-previews" : "")]![preview ? "post" : "put"]!;
            var media = operation["requestBody"]!["content"]!["application/json"]!;
            if (media["schema"]!["$ref"]?.GetValue<string>() is { } reference)
                media["schema"] = document["components"]!["schemas"]![reference.Split('/')[^1]]!.DeepClone();
            return operation;
        }

        public async Task<DomainEventRecord[]> EventsAsync()
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in _events.ReadAsync(new EventStreamAddress(Tenant.ToString(),
                               "applications", Application.ToString()), 0, CancellationToken.None))
                records.Add(record);
            return records.ToArray();
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _factory.DisposeAsync();
        }
    }

    sealed record BusCall(IRequestBase Request, RequestDispatchContext Context);

    sealed class ObservedBus(IRequestBus inner, List<BusCall> calls) : IRequestBus
    {
        public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null) =>
            inner.CreateContext(actor, metadata);
        public ValueTask<Result> AuthorizeAsync(IRequestBase request, RequestDispatchContext context, CancellationToken ct = default) =>
            inner.AuthorizeAsync(request, context, ct);
        public ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context, CancellationToken ct = default)
        {
            calls.Add(new(request, context));
            return inner.DispatchAsync(request, context, ct);
        }
        public ValueTask<Result<TOut>> DispatchAsync<TOut>(IRequest<TOut> request, RequestDispatchContext context, CancellationToken ct = default)
        {
            calls.Add(new(request, context));
            return inner.DispatchAsync(request, context, ct);
        }
        public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(IStreamRequest<TOut> request, RequestDispatchContext context,
            CancellationToken ct = default) => inner.DispatchStreamAsync(request, context, ct);
    }

    sealed class PreviewPermissions(bool allowed) : IAccessGrantPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) => ValueTask.FromResult(allowed);
        public ValueTask<ProgramAccessVisibility> GetProgramVisibilityAsync(Uuid tenantId,
            Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProgramAccessVisibility(allowed, new HashSet<Uuid>()));
    }

    sealed class BlockingReadStream : Stream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CancellationObserved { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return 0;
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = cancellationToken.IsCancellationRequested;
                throw;
            }
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    sealed class UnknownLengthContent(string json) : StringContent(json, Encoding.UTF8, "application/json")
    {
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
