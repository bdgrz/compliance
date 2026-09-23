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
public sealed class SnapshotE2ETests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
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
        IHost? replayWorker = null;
        if (splitWorker)
        {
            worker = BuildWorker(applicationName);
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

                var freezePath = $"/api/v1/tenants/{tenantId}/scope-snapshots";
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
                    $"{programPath}/scope-snapshots");
                Assert.Equal(HttpStatusCode.OK, beforeFreeze.StatusCode);
                var emptyHistory = await ReadAsync(beforeFreeze);
                Assert.Empty(emptyHistory.GetProperty("items").EnumerateArray());

                using var previewResponse = await owner.GetAsync(
                    $"{draftPath}/impact-preview?expected_revision=1");
                Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
                var preview = await ReadAsync(previewResponse);
                Assert.True(preview.GetProperty("complete").GetBoolean());
                var digest = preview.GetProperty("digest").GetString()!;

                var reviewerEmail = $"snapshot-reviewer-{Guid.NewGuid():N}@example.com";
                using var invitation = await owner.PostAsJsonAsync(
                    $"/api/v1/tenants/{tenantId}/invitations",
                    new { email_address = reviewerEmail, affiliation = "client_personnel", administrator = false });
                Assert.Equal(HttpStatusCode.NoContent, invitation.StatusCode);
                var delivery = splitWorker
                    ? worker!.Services.GetRequiredService<MockTenantInvitationDelivery>()
                    : factory.Services.GetRequiredService<MockTenantInvitationDelivery>();
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
                    reviewerId, reviewerEmail,
                    splitWorker ? worker!.Services.GetRequiredService<MockEmailChallengeDelivery>() : null);
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
                if (splitWorker)
                {
                    await worker!.StopAsync();
                    worker.Dispose();
                    worker = null;
                }
                using var frozenResponse = await owner.PostAsJsonAsync(freezePath, freezeBody);
                Assert.Equal(HttpStatusCode.OK, frozenResponse.StatusCode);
                var frozen = await ReadAsync(frozenResponse);
                var snapshotId = frozen.GetProperty("snapshot_id").GetString()!;
                var originalDigest = frozen.GetProperty("content_sha256").GetString()!;
                var snapshotPath = $"{freezePath}/{snapshotId}";
                var regenerationPath = $"/api/v1/tenants/{tenantId}/scope-snapshots/" +
                    $"{snapshotId}/manifest-regeneration";
                if (splitWorker)
                {
                    using var lagged = await owner.GetAsync($"{snapshotPath}?minimum_revision=1");
                    Assert.Equal(HttpStatusCode.Conflict, lagged.StatusCode);
                    using var sourceOnly = await owner.GetAsync(regenerationPath);
                    Assert.Equal(HttpStatusCode.OK, sourceOnly.StatusCode);
                    Assert.Equal(originalDigest,
                        (await ReadAsync(sourceOnly)).GetProperty("content_sha256").GetString());
                    await using var sourceOnlyMcp = await McpScenario.ConnectAsync(owner,
                        new Uri(owner.BaseAddress!, "/mcp"));
                    _ = await sourceOnlyMcp.When(
                        "bdgrz.snapshot.program_scope.manifest_regenerate",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = tenantId,
                            ["snapshot_id"] = snapshotId,
                        }).ExpectSuccess();
                    replayWorker = BuildWorker(applicationName);
                    await replayWorker.StartAsync();
                }
                var original = await WaitForAsync(owner,
                    $"{snapshotPath}?minimum_revision=1",
                    static view => view.GetProperty("revision").GetInt64() == 1);
                var verificationPath = $"{snapshotPath}/verification";
                var verification = await WaitForAsync(owner, verificationPath,
                    static view => view.GetProperty("verified").GetBoolean());
                using var regeneratedResponse = await owner.GetAsync(regenerationPath);
                Assert.Equal(HttpStatusCode.OK, regeneratedResponse.StatusCode);
                var regenerated = await ReadAsync(regeneratedResponse);

                // Assert
                Assert.Equal(64, originalDigest.Length);
                Assert.Equal("program_scope", original.GetProperty("kind").GetString());
                Assert.Equal(1, original.GetProperty("manifest")
                    .GetProperty("program_revision").GetInt64());
                Assert.Equal(originalDigest, original.GetProperty("content_sha256").GetString());
                Assert.Equal(original.GetProperty("canonical_manifest").GetString(),
                    regenerated.GetProperty("canonical_manifest").GetString());
                Assert.Equal(originalDigest,
                    regenerated.GetProperty("content_sha256").GetString());
                Assert.Equal("verified", verification.GetProperty("snapshot")
                    .GetProperty("status").GetString());
                Assert.Equal("verified", verification.GetProperty("program_revision")
                    .GetProperty("status").GetString());
                Assert.Equal("verified", verification.GetProperty("approved_boundary_version")
                    .GetProperty("status").GetString());
                using var listedResponse = await owner.GetAsync(
                    $"{programPath}/scope-snapshots");
                Assert.Equal(HttpStatusCode.OK, listedResponse.StatusCode);
                var listed = await ReadAsync(listedResponse);
                Assert.Contains(listed.GetProperty("items").EnumerateArray(), item =>
                    item.GetProperty("snapshot_id").GetString() == snapshotId);
                using var deniedRead = await outsider.GetAsync(snapshotPath);
                using var deniedVerification = await outsider.GetAsync(verificationPath);
                using var deniedRegeneration = await outsider.GetAsync(regenerationPath);
                using var deniedList = await outsider.GetAsync(
                    $"{programPath}/scope-snapshots");
                using var deniedFreeze = await outsider.PostAsJsonAsync(freezePath, freezeBody);
                Assert.Equal(HttpStatusCode.NotFound, deniedRead.StatusCode);
                Assert.Equal(deniedRead.StatusCode, deniedVerification.StatusCode);
                Assert.Equal(deniedRead.StatusCode, deniedRegeneration.StatusCode);
                Assert.Equal(deniedRead.StatusCode, deniedList.StatusCode);
                Assert.Equal(deniedRead.StatusCode, deniedFreeze.StatusCode);
                using var secondTenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants",
                    new
                    {
                        name = "Second snapshot tenant",
                        slug = $"snapshot-other-{Guid.NewGuid():N}"[..24],
                    });
                Assert.Equal(HttpStatusCode.OK, secondTenantResponse.StatusCode);
                var secondTenant = await ReadAsync(secondTenantResponse);
                var secondTenantId = secondTenant.GetProperty("tenant_id").GetString()!;
                using var foreignRealm = await owner.GetAsync(
                    $"/api/v1/tenants/{secondTenantId}/scope-snapshots/{snapshotId}/verification");
                using var foreignRegeneration = await owner.GetAsync(
                    $"/api/v1/tenants/{secondTenantId}/scope-snapshots/{snapshotId}/manifest-regeneration");
                Assert.Equal(HttpStatusCode.NotFound, foreignRealm.StatusCode);
                Assert.Equal(foreignRealm.StatusCode, foreignRegeneration.StatusCode);
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
                    _ = await ownerMcp.When("bdgrz.snapshot.program_scope.verify",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = tenantId,
                            ["snapshot_id"] = snapshotId,
                        }).ExpectSuccess();
                    _ = await ownerMcp.When(
                        "bdgrz.snapshot.program_scope.manifest_regenerate",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = tenantId,
                            ["snapshot_id"] = snapshotId,
                        }).ExpectSuccess();
                    _ = await ownerMcp.When(
                        "bdgrz.snapshot.program_scope.manifest_regenerate",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = secondTenantId,
                            ["snapshot_id"] = snapshotId,
                        }).ExpectFailure();
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
                    _ = await outsiderMcp.When("bdgrz.snapshot.program_scope.verify",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = tenantId,
                            ["snapshot_id"] = snapshotId,
                        }).ExpectFailure();
                    _ = await outsiderMcp.When(
                        "bdgrz.snapshot.program_scope.manifest_regenerate",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = tenantId,
                            ["snapshot_id"] = snapshotId,
                        }).ExpectFailure();
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
                var boundedPredecessorId = amendedId;
                var amendmentBody = new
                {
                    program_id = programId,
                    expected_program_revision = 2,
                    boundary_id = boundaryId,
                    approved_boundary_version_id = versionId,
                    reason = "A bounded correction.",
                };
                for (var amendmentNumber = 2; amendmentNumber <= 8; amendmentNumber++)
                {
                    using var boundedAmendment = await owner.PostAsJsonAsync(
                        $"{freezePath}/{boundedPredecessorId}/amendments", amendmentBody);
                    Assert.Equal(HttpStatusCode.OK, boundedAmendment.StatusCode);
                    boundedPredecessorId = (await ReadAsync(boundedAmendment))
                        .GetProperty("snapshot_id").GetString()!;
                }
                using var overLimit = await owner.PostAsJsonAsync(
                    $"{freezePath}/{boundedPredecessorId}/amendments", amendmentBody);
                Assert.Equal(HttpStatusCode.Conflict, overLimit.StatusCode);
                var boundedRegenerationPath =
                    $"/api/v1/tenants/{tenantId}/scope-snapshots/{boundedPredecessorId}/" +
                    "manifest-regeneration";
                await using var boundedMcp = await McpScenario.ConnectAsync(owner,
                    new Uri(owner.BaseAddress!, "/mcp"));
                var concurrentHttp = owner.GetAsync(boundedRegenerationPath);
                var concurrentMcp = Task.Run(async () =>
                {
                    _ = await boundedMcp.When(
                        "bdgrz.snapshot.program_scope.manifest_regenerate",
                        new Dictionary<string, object?>
                        {
                            ["tenant_id"] = tenantId,
                            ["snapshot_id"] = boundedPredecessorId,
                        }).ExpectSuccess();
                });
                await Task.WhenAll(concurrentHttp, concurrentMcp);
                using var boundedRegeneration = await concurrentHttp;
                Assert.Equal(HttpStatusCode.OK, boundedRegeneration.StatusCode);
                using var originalAgain = await owner.GetAsync(snapshotPath);
                Assert.Equal(HttpStatusCode.OK, originalAgain.StatusCode);
                var unchanged = await ReadAsync(originalAgain);
                Assert.Equal(originalDigest, unchanged.GetProperty("content_sha256").GetString());
                using var regeneratedOriginal = await owner.GetAsync(regenerationPath);
                Assert.Equal(HttpStatusCode.OK, regeneratedOriginal.StatusCode);
                Assert.Equal(original.GetProperty("canonical_manifest").GetString(),
                    (await ReadAsync(regeneratedOriginal)).GetProperty("canonical_manifest")
                    .GetString());
                using var verifiedAfterAmendment = await owner.GetAsync(verificationPath);
                Assert.Equal(HttpStatusCode.OK, verifiedAfterAmendment.StatusCode);
                Assert.True((await ReadAsync(verifiedAfterAmendment))
                    .GetProperty("verified").GetBoolean());
            }
        }
        finally
        {
            if (replayWorker is not null)
            {
                await replayWorker.StopAsync();
                replayWorker.Dispose();
            }
            if (worker is not null)
            {
                await worker.StopAsync();
                worker.Dispose();
            }
        }
    }

    IHost BuildWorker(string applicationName)
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
        return builder.Build();
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
