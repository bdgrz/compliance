using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class SnapshotE2ETests(BrokerStackFixture broker)
{
    static readonly string[] SecurityCategory = ["security"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldFreezeExactScopeGivenStandaloneOrSplitWorker(bool splitWorker)
    {
        // Arrange
        var applicationName = $"compliance-snapshot-{Guid.NewGuid():N}";
        IHost? worker = null;
        if (splitWorker)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                EnvironmentName = "Development",
            });
            builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
            builder.Configuration["Fitz:ApplicationName"] = applicationName;
            builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
            builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true)
                .AddWorkers();
            worker = builder.Build();
            await worker.StartAsync();
        }

        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient owner;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE",
                    splitWorker ? "api" : "standalone");
                owner = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
            }

            using (owner)
            using (var outsider = factory.CreateClient())
            {
                await TenantInvitationE2ETests.LoginAsync(owner,
                    $"snapshot-owner-{Guid.NewGuid():N}@example.com");
                await TenantInvitationE2ETests.LoginAsync(outsider,
                    $"snapshot-outsider-{Guid.NewGuid():N}@example.com");
                using var createdTenant = await owner.PostAsJsonAsync("/api/v1/tenants", new
                {
                    name = "Snapshot E2E",
                    slug = $"snapshot-{Guid.NewGuid():N}"[..24],
                });
                Assert.Equal(HttpStatusCode.OK, createdTenant.StatusCode);
                var tenant = await ReadAsync(createdTenant);
                var tenantId = tenant.GetProperty("tenant_id").GetString()!;
                var programsPath = $"/api/v1/tenants/{tenantId}/programs";
                var plan = new
                {
                    target_readiness_date = "2027-01-31",
                    target_type_i_as_of_date = "2027-03-31",
                    target_type_ii_start_date = "2027-04-01",
                    target_type_ii_end_date = "2028-03-31",
                    readiness_advisor = "Advisor A",
                    audit_firm = (string?)null,
                };
                var programId = await CreateProgramAsync(owner, programsPath, plan);
                var programPath = $"{programsPath}/{programId}";
                _ = await WaitForAsync(owner, programPath,
                    static view => view.GetProperty("revision").GetInt64() == 1);

                var boundariesPath = $"{programPath}/boundaries";
                using var createdBoundary = await owner.PostAsJsonAsync(boundariesPath, new
                {
                    content = new
                    {
                        statement = "The first client service is in scope.",
                        engagement_stage = "readiness",
                        trust_services_categories = SecurityCategory,
                        entries = new[]
                        {
                            new
                            {
                                entry_id = Uuid.CreateVersion4(),
                                kind = "inclusion",
                                subject_type = "service",
                                subject = "Client service",
                                governed_record_id = (string?)null,
                                owner_reference = "Compliance lead",
                                rationale = "Ownership is being confirmed.",
                                unresolved = true,
                            },
                        },
                    },
                });
                Assert.Equal(HttpStatusCode.OK, createdBoundary.StatusCode);
                var boundary = await ReadAsync(createdBoundary);
                var boundaryId = boundary.GetProperty("boundary_id").GetString()!;
                var versionId = boundary.GetProperty("draft_version_id").GetString()!;
                var boundaryPath = $"/api/v1/tenants/{tenantId}/boundaries/{boundaryId}";
                var draftPath = $"{boundaryPath}/drafts/{versionId}";
                _ = await WaitForAsync(owner, boundaryPath,
                    static view => view.GetProperty("draft").ValueKind == JsonValueKind.Object);

                var freezePath = $"/api/v1/tenants/{tenantId}/scope_snapshots";
                var freezeBody = new
                {
                    program_id = programId,
                    expected_program_revision = 1,
                    boundary_id = boundaryId,
                    approved_boundary_version_id = versionId,
                };
                using var incomplete = await owner.PostAsJsonAsync(freezePath, freezeBody);
                Assert.Equal(HttpStatusCode.NotFound, incomplete.StatusCode);
                using var beforeFreeze = await owner.GetAsync(
                    $"{programPath}/scope_snapshots");
                Assert.Equal(HttpStatusCode.OK, beforeFreeze.StatusCode);
                var emptyHistory = await ReadAsync(beforeFreeze);
                Assert.Empty(emptyHistory.GetProperty("items").EnumerateArray());

                using var previewResponse = await owner.GetAsync(
                    $"{draftPath}/impact_preview?expected_revision=1");
                Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
                var preview = await ReadAsync(previewResponse);
                Assert.True(preview.GetProperty("complete").GetBoolean());
                var digest = preview.GetProperty("digest").GetString()!;

                var reviewerEmail = $"snapshot-reviewer-{Guid.NewGuid():N}@example.com";
                using var invitation = await owner.PostAsJsonAsync(
                    $"/api/v1/tenants/{tenantId}/invitations",
                    new { email_address = reviewerEmail, affiliation = "client_personnel", administrator = false });
                Assert.Equal(HttpStatusCode.NoContent, invitation.StatusCode);
                var delivery = factory.Services.GetRequiredService<MockTenantInvitationDelivery>();
                string? token = null;
                var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
                while (DateTimeOffset.UtcNow < deadline &&
                       !delivery.TryGetLatest(Uuid.Parse(tenantId, CultureInfo.InvariantCulture),
                           reviewerEmail, out token))
                    await Task.Delay(250);
                Assert.NotNull(token);
                using var reviewer = factory.CreateClient();
                var reviewerId = await TenantInvitationE2ETests.LoginAsync(reviewer,
                    reviewerEmail);
                await TenantInvitationE2ETests.VerifyEmailAsync(factory, reviewer,
                    reviewerId, reviewerEmail);
                using var accepted = await reviewer.PostAsJsonAsync(
                    $"/api/v1/tenants/{tenantId}/invitations/acceptance",
                    new { email_address = reviewerEmail, token });
                Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
                var parsedTenantId = Uuid.Parse(tenantId, CultureInfo.InvariantCulture);
                var reviewerMemberId = RbacIds.Member(parsedTenantId,
                    Uuid.Parse(reviewerId, CultureInfo.InvariantCulture));
                using var assigned = await owner.PostAsync(
                    $"/api/v1/tenants/{tenantId}/teams/" +
                    $"{BuiltInRbac.PowerUsersTeamId(parsedTenantId)}/members/{reviewerMemberId}",
                    null);
                Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);

                await PostUntilNoContentAsync(reviewer, $"{draftPath}/reviews", new
                {
                    expected_revision = 1,
                    outcome = "accept",
                    rationale = "The initial scope is understood.",
                });
                var reviewed = await WaitForAsync(owner, boundaryPath,
                    static view => view.GetProperty("latest_decision").ValueKind ==
                                   JsonValueKind.Object);
                var decisionId = reviewed.GetProperty("latest_decision")
                    .GetProperty("decision_id").GetString()!;
                using var approved = await reviewer.PostAsJsonAsync(
                    $"{draftPath}/approvals", new
                    {
                        expected_revision = 1,
                        accepted_review_decision_id = decisionId,
                        effective_from = "2027-01-01",
                        rationale = "Approved for scoped planning.",
                        impact_digest = digest,
                    });
                Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);
                _ = await WaitForAsync(owner, $"{boundaryPath}/versions/{versionId}",
                    static view => view.GetProperty("status").GetString() == "approved");

                // Act
                using var frozenResponse = await owner.PostAsJsonAsync(freezePath, freezeBody);
                Assert.Equal(HttpStatusCode.OK, frozenResponse.StatusCode);
                var frozen = await ReadAsync(frozenResponse);
                var snapshotId = frozen.GetProperty("snapshot_id").GetString()!;
                var originalDigest = frozen.GetProperty("content_sha256").GetString()!;
                var snapshotPath = $"{freezePath}/{snapshotId}";
                var original = await WaitForAsync(owner,
                    $"{snapshotPath}?minimum_revision=1",
                    static view => view.GetProperty("revision").GetInt64() == 1);

                // Assert
                Assert.Equal(64, originalDigest.Length);
                Assert.Equal("program_scope", original.GetProperty("kind").GetString());
                Assert.Equal(1, original.GetProperty("manifest")
                    .GetProperty("program_revision").GetInt64());
                Assert.Equal(originalDigest, original.GetProperty("content_sha256").GetString());
                using var listedResponse = await owner.GetAsync(
                    $"{programPath}/scope_snapshots");
                Assert.Equal(HttpStatusCode.OK, listedResponse.StatusCode);
                var listed = await ReadAsync(listedResponse);
                Assert.Contains(listed.GetProperty("items").EnumerateArray(), item =>
                    item.GetProperty("snapshot_id").GetString() == snapshotId);
                using var deniedRead = await outsider.GetAsync(snapshotPath);
                using var deniedList = await outsider.GetAsync(
                    $"{programPath}/scope_snapshots");
                using var deniedFreeze = await outsider.PostAsJsonAsync(freezePath, freezeBody);
                Assert.Equal(HttpStatusCode.NotFound, deniedRead.StatusCode);
                Assert.Equal(deniedRead.StatusCode, deniedList.StatusCode);
                Assert.Equal(deniedRead.StatusCode, deniedFreeze.StatusCode);
                using var future = await owner.GetAsync($"{snapshotPath}?minimum_revision=2");
                Assert.Equal(HttpStatusCode.Conflict, future.StatusCode);
                using var invalid = await owner.GetAsync($"{snapshotPath}?minimum_revision=0");
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

                var input = new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenantId,
                    ["snapshot_id"] = snapshotId,
                    ["minimum_revision"] = 1,
                };
                await using (var ownerMcp = await McpScenario.ConnectAsync(owner,
                                 new Uri(owner.BaseAddress!, "/mcp")))
                {
                    _ = await ownerMcp.When("bdgrz.snapshot.get", input).ExpectSuccess();
                    _ = await ownerMcp.When("bdgrz.snapshot.program_scope.freeze",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = tenantId,
                            ["program_id"] = programId,
                            ["expected_program_revision"] = 1,
                            ["boundary_id"] = boundaryId,
                            ["approved_boundary_version_id"] = versionId,
                        }).ExpectSuccess();
                }
                await using (var outsiderMcp = await McpScenario.ConnectAsync(outsider,
                                 new Uri(outsider.BaseAddress!, "/mcp")))
                {
                    _ = await outsiderMcp.When("bdgrz.snapshot.get", input).ExpectFailure();
                    _ = await outsiderMcp.When("bdgrz.snapshot.program_scope.freeze",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = tenantId,
                            ["program_id"] = programId,
                            ["expected_program_revision"] = 1,
                            ["boundary_id"] = boundaryId,
                            ["approved_boundary_version_id"] = versionId,
                        }).ExpectFailure();
                }

                using var revised = await owner.PutAsJsonAsync(programPath, new
                {
                    expected_revision = 1,
                    name = "Revised program name",
                    plan,
                });
                Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
                _ = await WaitForAsync(owner, $"{programPath}/revisions/2",
                    static view => view.GetProperty("revision").GetInt64() == 2);
                using var amendedResponse = await owner.PostAsJsonAsync(
                    $"{snapshotPath}/amendments", new
                    {
                        program_id = programId,
                        expected_program_revision = 2,
                        boundary_id = boundaryId,
                        approved_boundary_version_id = versionId,
                        reason = "Updated the program plan.",
                    });
                Assert.Equal(HttpStatusCode.OK, amendedResponse.StatusCode);
                var amended = await ReadAsync(amendedResponse);
                var amendedId = amended.GetProperty("snapshot_id").GetString()!;
                var amendedView = await WaitForAsync(owner, $"{freezePath}/{amendedId}",
                    static view => view.GetProperty("revision").GetInt64() == 1);
                Assert.Equal(snapshotId, amendedView.GetProperty("root_snapshot_id").GetString());
                Assert.Equal(snapshotId, amendedView.GetProperty("amends_snapshot_id").GetString());
                Assert.NotEqual(originalDigest,
                    amendedView.GetProperty("content_sha256").GetString());
                using var originalAgain = await owner.GetAsync(snapshotPath);
                Assert.Equal(HttpStatusCode.OK, originalAgain.StatusCode);
                var unchanged = await ReadAsync(originalAgain);
                Assert.Equal(originalDigest, unchanged.GetProperty("content_sha256").GetString());
            }
        }
        finally
        {
            if (worker is not null)
            {
                await worker.StopAsync();
                worker.Dispose();
            }
        }
    }

    static async Task<string> CreateProgramAsync(HttpClient owner, string path, object plan)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path,
                new { name = "Snapshot program", plan });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var created = await ReadAsync(response);
                return created.GetProperty("program_id").GetString()!;
            }
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new InvalidOperationException("The program could not be created before the deadline.");
    }

    static async Task PostUntilNoContentAsync(HttpClient client, string path, object body)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.PostAsJsonAsync(path, body);
            if (response.StatusCode == HttpStatusCode.NoContent)
                return;
            Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new InvalidOperationException("The command did not succeed before the deadline.");
    }

    static async Task<JsonElement> WaitForAsync(HttpClient client, string path,
        Func<JsonElement, bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var view = await ReadAsync(response);
                if (predicate(view))
                    return view;
            }
            else
                Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict,
                    await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new InvalidOperationException("The projection did not catch up before the deadline.");
    }

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
