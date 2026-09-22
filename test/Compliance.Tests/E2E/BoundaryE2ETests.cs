using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Fitz;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BoundaryBrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class BoundaryE2ETests(BrokerStackFixture broker)
{
    [Fact]
    public async Task ShouldProjectTenantScopedDraftGivenBoundaryCreationAndRevision()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"boundary-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"boundary-outsider-{Guid.NewGuid():N}@example.com");


        // Act
        using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Boundary E2E",
            slug = $"boundary-{Guid.NewGuid():N}"[..24],
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
        var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantDocument>();
        Assert.NotNull(tenant);
        var programsPath = $"/api/v1/tenants/{tenant.TenantId}/programs";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        ProgramDocument? program = null;
        string? lastBootstrapResponse = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(programsPath, new
            {
                name = "Readiness program",
                plan = new
                {
                    target_readiness_date = "2027-01-31",
                    target_type_i_as_of_date = "2027-03-31",
                    target_type_ii_start_date = "2027-04-01",
                    target_type_ii_end_date = "2028-03-31",
                    readiness_advisor = "Advisor",
                    audit_firm = (string?)null,
                },
            });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                program = await response.Content.ReadFromJsonAsync<ProgramDocument>();
                break;
            }
            lastBootstrapResponse = $"{(int)response.StatusCode} " +
                                    await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                lastBootstrapResponse);
            await Task.Delay(250);
        }
        Assert.True(program is not null,
            $"Program creation remained unavailable after tenant bootstrap; last response: {lastBootstrapResponse}");
        Assert.NotNull(program);
        var path = $"{programsPath}/{program.ProgramId}/boundaries";
        var entryId = Uuid.CreateVersion4();
        var content = new
        {
            statement = "Service A handles customer work.",
            engagement_stage = "readiness",
            trust_services_categories = new[] { "security" },
            entries = new[]
            {
                new
                {
                    entry_id = entryId.ToString(),
                    kind = "inclusion",
                    subject_type = "service",
                    subject = "Service A",
                    governed_record_id = (string?)null,
                    owner_reference = "Compliance lead",
                    rationale = "It processes customer data.",
                    unresolved = true,
                },
            },
        };
        BoundaryRegistrationDocument? registration = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(path, new { content });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                registration = await response.Content.ReadFromJsonAsync<BoundaryRegistrationDocument>();
                break;
            }
            Assert.True(response.StatusCode == HttpStatusCode.NotFound,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        Assert.NotNull(registration);
        var boundaryPath = $"/api/v1/tenants/{tenant.TenantId}/boundaries/{registration.BoundaryId}";
        BoundaryDocument? projected = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(boundaryPath);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                projected = await response.Content.ReadFromJsonAsync<BoundaryDocument>();
                if (projected?.Draft?.Revision == 1)
                    break;
            }
            await Task.Delay(250);
        }
        Assert.Equal(registration.DraftVersionId, projected?.Draft?.VersionId);
        Assert.Equal(1, projected?.Revision);
        Assert.Equal("Service A handles customer work.", projected?.Draft?.Content.Statement);
        using var currentRevision = await owner.GetAsync($"{boundaryPath}?minimum_revision=1");
        using var futureRevision = await owner.GetAsync($"{boundaryPath}?minimum_revision=2");
        using var invalidRevision = await owner.GetAsync($"{boundaryPath}?minimum_revision=0");
        using var missingRevision = await owner.GetAsync(
            $"/api/v1/tenants/{tenant.TenantId}/boundaries/{Uuid.CreateVersion4()}?minimum_revision=1");
        using var deniedRevision = await outsider.GetAsync($"{boundaryPath}?minimum_revision=1");
        Assert.Equal(HttpStatusCode.OK, currentRevision.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, futureRevision.StatusCode);
        Assert.Contains("source has not reached revision 2",
            await futureRevision.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRevision.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingRevision.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedRevision.StatusCode);
        using var programBoundaries = await owner.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, programBoundaries.StatusCode);
        var boundaryList = await programBoundaries.Content
            .ReadFromJsonAsync<BoundaryPageDocument>();
        Assert.Equal(registration.BoundaryId,
            Assert.Single(boundaryList?.Items ?? []).BoundaryId);
        using var deniedList = await outsider.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, deniedList.StatusCode);
        using var unsupportedGovernedLink = await owner.PostAsJsonAsync(path, new
        {
            content = new
            {
                content.statement,
                content.engagement_stage,
                content.trust_services_categories,
                entries = new[]
                {
                    new
                    {
                        entry_id = Uuid.CreateVersion4().ToString(),
                        kind = "inclusion",
                        subject_type = "service",
                        subject = "Unverified service",
                        governed_record_id = Uuid.CreateVersion4().ToString(),
                        owner_reference = "Compliance lead",
                        rationale = "Claimed as governed without an inventory.",
                        unresolved = false,
                    },
                },
            },
        });
        Assert.Equal(HttpStatusCode.Conflict, unsupportedGovernedLink.StatusCode);
        var draftPath = $"{boundaryPath}/drafts/{registration.DraftVersionId}";
        using var initialPreviewResponse = await owner.GetAsync(
            $"{draftPath}/impact-preview?expected_revision=1");
        Assert.Equal(HttpStatusCode.OK, initialPreviewResponse.StatusCode);
        var initialPreview = await initialPreviewResponse.Content
            .ReadFromJsonAsync<BoundaryImpactPreviewDocument>();
        Assert.True(initialPreview?.Complete);
        Assert.Contains(initialPreview?.Changes ?? [], change =>
            change.Field == "scope_entry" && change.ChangeType == "added");
        using var deniedPreview = await outsider.GetAsync(
            $"{draftPath}/impact-preview?expected_revision=1");
        Assert.Equal(HttpStatusCode.NotFound, deniedPreview.StatusCode);

        using var deniedRead = await outsider.GetAsync(boundaryPath);
        using var missingRead = await outsider.GetAsync(
            $"/api/v1/tenants/{tenant.TenantId}/boundaries/{Uuid.CreateVersion4()}");
        using var deniedCreate = await outsider.PostAsJsonAsync(path, new { content });
        Assert.Equal(HttpStatusCode.NotFound, deniedRead.StatusCode);
        Assert.Equal(missingRead.StatusCode, deniedRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedCreate.StatusCode);

        using var revised = await owner.PutAsJsonAsync(draftPath, new
        {
            expected_revision = 1,
            content = new
            {
                statement = "Service A and its provider are in scope.",
                content.engagement_stage,
                content.trust_services_categories,
                content.entries,
            },
        });
        Assert.Equal(HttpStatusCode.NoContent, revised.StatusCode);
        using var stale = await owner.PutAsJsonAsync(draftPath, new
        {
            expected_revision = 1,
            content,
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var deniedRevise = await outsider.PutAsJsonAsync(draftPath, new
        {
            expected_revision = 2,
            content,
        });
        Assert.Equal(HttpStatusCode.NotFound, deniedRevise.StatusCode);
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(boundaryPath);
            projected = await response.Content.ReadFromJsonAsync<BoundaryDocument>();
            if (projected?.Draft?.Revision == 2)
                break;
            await Task.Delay(250);
        }
        Assert.Equal(2, projected?.Draft?.Revision);
        Assert.Equal(2, projected?.Revision);
        using var revisedRevision = await owner.GetAsync($"{boundaryPath}?minimum_revision=2");
        Assert.Equal(HttpStatusCode.OK, revisedRevision.StatusCode);
        Assert.Equal("Service A and its provider are in scope.", projected?.Draft?.Content.Statement);
        using var stalePreview = await owner.GetAsync(
            $"{draftPath}/impact-preview?expected_revision=1");
        Assert.Equal(HttpStatusCode.Conflict, stalePreview.StatusCode);
        using var reviewedPreviewResponse = await owner.GetAsync(
            $"{draftPath}/impact-preview?expected_revision=2");
        Assert.Equal(HttpStatusCode.OK, reviewedPreviewResponse.StatusCode);
        var reviewedPreview = await reviewedPreviewResponse.Content
            .ReadFromJsonAsync<BoundaryImpactPreviewDocument>();
        Assert.NotNull(reviewedPreview);

        using var selfReview = await owner.PostAsJsonAsync($"{draftPath}/reviews", new
        {
            expected_revision = 2,
            outcome = "accept",
            rationale = "The boundary is complete.",
        });
        Assert.Equal(HttpStatusCode.Forbidden, selfReview.StatusCode);

        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        var reviewerEmail = $"boundary-reviewer-{Guid.NewGuid():N}@example.com";
        using var invitation = await owner.PostAsJsonAsync(
            $"/api/v1/tenants/{tenant.TenantId}/invitations",
            new { email_address = reviewerEmail, affiliation = "client_personnel", administrator = false });
        Assert.Equal(HttpStatusCode.NoContent, invitation.StatusCode);
        var delivery = factory.Services.GetRequiredService<MockTenantInvitationDelivery>();
        string? token = null;
        while (DateTimeOffset.UtcNow < deadline &&
               !delivery.TryGetLatest(Uuid.Parse(tenant.TenantId, CultureInfo.InvariantCulture),
                   reviewerEmail, out token))
            await Task.Delay(250);
        Assert.NotNull(token);
        using var reviewer = factory.CreateClient();
        var reviewerId = await TenantInvitationE2ETests.LoginAsync(reviewer, reviewerEmail);
        await TenantInvitationE2ETests.VerifyEmailAsync(factory, reviewer, reviewerId, reviewerEmail);
        using var accepted = await reviewer.PostAsJsonAsync(
            $"/api/v1/tenants/{tenant.TenantId}/invitations/acceptance",
            new { email_address = reviewerEmail, token });
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

        var tenantId = Uuid.Parse(tenant.TenantId, CultureInfo.InvariantCulture);
        var reviewerMemberId = RbacIds.Member(tenantId,
            Uuid.Parse(reviewerId, CultureInfo.InvariantCulture));
        using var assigned = await owner.PostAsync(
            $"/api/v1/tenants/{tenant.TenantId}/teams/" +
            $"{BuiltInRbac.PowerUsersTeamId(tenantId)}/members/{reviewerMemberId}", null);
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);

        HttpResponseMessage? reviewed = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            reviewed = await reviewer.PostAsJsonAsync($"{draftPath}/reviews", new
            {
                expected_revision = 2,
                outcome = "accept",
                rationale = "The scope statement identifies the service and owner.",
            });
            if (reviewed.StatusCode == HttpStatusCode.NoContent)
                break;
            Assert.True(reviewed.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                await reviewed.Content.ReadAsStringAsync());
            reviewed.Dispose();
            await Task.Delay(250);
        }
        Assert.NotNull(reviewed);
        using (reviewed)
            Assert.Equal(HttpStatusCode.NoContent, reviewed.StatusCode);

        BoundaryDecisionDocument? decision = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(boundaryPath);
            var view = await response.Content.ReadFromJsonAsync<BoundaryDocument>();
            decision = view?.LatestDecision;
            if (decision?.Outcome == "accept")
                break;
            await Task.Delay(250);
        }
        Assert.NotNull(decision);
        using var selfApproval = await owner.PostAsJsonAsync($"{draftPath}/approvals", new
        {
            expected_revision = 2,
            accepted_review_decision_id = decision.DecisionId,
            effective_from = "2027-01-01",
            rationale = "Approved for readiness.",
            impact_digest = reviewedPreview.Digest,
        });
        Assert.Equal(HttpStatusCode.Forbidden, selfApproval.StatusCode);
        using var staleImpact = await reviewer.PostAsJsonAsync($"{draftPath}/approvals", new
        {
            expected_revision = 2,
            accepted_review_decision_id = decision.DecisionId,
            effective_from = "2027-01-01",
            rationale = "Approved for readiness.",
            impact_digest = "stale",
        });
        Assert.Equal(HttpStatusCode.Conflict, staleImpact.StatusCode);
        using var approved = await reviewer.PostAsJsonAsync($"{draftPath}/approvals", new
        {
            expected_revision = 2,
            accepted_review_decision_id = decision.DecisionId,
            effective_from = "2027-01-01",
            rationale = "Approved for readiness.",
            impact_digest = reviewedPreview.Digest,
        });
        Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(boundaryPath);
            projected = await response.Content.ReadFromJsonAsync<BoundaryDocument>();
            if (projected?.LatestApprovedVersion?.Status == "approved")
                break;
            await Task.Delay(250);
        }
        Assert.Null(projected?.Draft);
        Assert.Equal(registration.DraftVersionId, projected?.LatestApprovedVersion?.VersionId);
        using var versionResponse = await owner.GetAsync(
            $"{boundaryPath}/versions/{registration.DraftVersionId}?minimum_boundary_revision=4");
        Assert.Equal(HttpStatusCode.OK, versionResponse.StatusCode);
        var version = await versionResponse.Content.ReadFromJsonAsync<BoundaryVersionDocument>();
        Assert.Equal("approved", version?.Status);
        Assert.Equal("2027-01-01", version?.EffectiveFrom);
        Assert.Equal("Service A and its provider are in scope.", version?.Content.Statement);
        using var versionsResponse = await owner.GetAsync(
            $"{boundaryPath}/versions?minimum_boundary_revision=4");
        Assert.Equal(HttpStatusCode.OK, versionsResponse.StatusCode);
        var versions = await versionsResponse.Content.ReadFromJsonAsync<BoundaryVersionPageDocument>();
        Assert.Equal(registration.DraftVersionId, Assert.Single(versions?.Items ?? []).VersionId);
        using var effective = await owner.GetAsync(
            $"{boundaryPath}/effective-version?effective_on=2027-01-01&minimum_boundary_revision=4");
        Assert.True(effective.StatusCode == HttpStatusCode.OK,
            await effective.Content.ReadAsStringAsync());
        var effectiveVersion = await effective.Content.ReadFromJsonAsync<BoundaryVersionDocument>();
        Assert.Equal(registration.DraftVersionId, effectiveVersion?.VersionId);
        foreach (var route in new[]
                 {
                     $"{boundaryPath}/versions/{registration.DraftVersionId}",
                     $"{boundaryPath}/versions",
                     $"{boundaryPath}/effective-version?effective_on=2027-01-01",
                 })
        {
            using var future = await owner.GetAsync(route +
                (route.Contains('?') ? "&" : "?") +
                "minimum_boundary_revision=5");
            using var invalid = await owner.GetAsync(route +
                (route.Contains('?') ? "&" : "?") +
                "minimum_boundary_revision=0");
            using var denied = await outsider.GetAsync(route +
                (route.Contains('?') ? "&" : "?") +
                "minimum_boundary_revision=4");
            Assert.Equal(HttpStatusCode.Conflict, future.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        using var absentVersion = await owner.GetAsync(
            $"{boundaryPath}/versions/{Uuid.CreateVersion4()}?minimum_boundary_revision=4");
        using var absentBoundary = await owner.GetAsync(
            $"/api/v1/tenants/{tenant.TenantId}/boundaries/{Uuid.CreateVersion4()}/versions" +
            "?minimum_boundary_revision=4");
        Assert.Equal(HttpStatusCode.NotFound, absentVersion.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, absentBoundary.StatusCode);
        await using (var mcp = await McpScenario.ConnectAsync(owner,
                         new Uri(owner.BaseAddress!, "/mcp")))
        {
            var anchor = new Dictionary<string, object?>
            {
                ["tenant_id"] = tenant.TenantId,
                ["boundary_id"] = registration.BoundaryId,
                ["minimum_boundary_revision"] = 4,
            };
            _ = await mcp.When("bdgrz.boundary.version.get",
                new Dictionary<string, object?>(anchor)
                {
                    ["version_id"] = registration.DraftVersionId,
                }).ExpectSuccess();
            _ = await mcp.When("bdgrz.boundary.versions.list", anchor).ExpectSuccess();
            _ = await mcp.When("bdgrz.boundary.version.effective.get",
                new Dictionary<string, object?>(anchor)
                {
                    ["effective_on"] = "2027-01-01",
                }).ExpectSuccess();
        }
        await using (var mcp = await McpScenario.ConnectAsync(outsider,
                         new Uri(outsider.BaseAddress!, "/mcp")))
            _ = await mcp.When("bdgrz.boundary.version.get",
                new Dictionary<string, object?>
                {
                    ["tenant_id"] = tenant.TenantId,
                    ["boundary_id"] = registration.BoundaryId,
                    ["version_id"] = registration.DraftVersionId,
                    ["minimum_boundary_revision"] = 4,
                }).ExpectFailure();
        using var beforeEffective = await owner.GetAsync(
            $"{boundaryPath}/effective-version?effective_on=2026-12-31");
        Assert.Equal(HttpStatusCode.NotFound, beforeEffective.StatusCode);
        using var deniedEffective = await outsider.GetAsync(
            $"{boundaryPath}/effective-version?effective_on=2027-01-01");
        Assert.Equal(HttpStatusCode.NotFound, deniedEffective.StatusCode);
        using var decisionsResponse = await owner.GetAsync($"{boundaryPath}/decisions");
        Assert.Equal(HttpStatusCode.OK, decisionsResponse.StatusCode);
        var decisions = await decisionsResponse.Content.ReadFromJsonAsync<BoundaryDecisionPageDocument>();
        Assert.Equal(["accept", "approve"], decisions?.Items.Select(item => item.Outcome));
        Assert.Equal(decision.DecisionId, decisions?.Items[1].ReliesOnDecisionId);
        Assert.Equal(reviewedPreview.Digest, decisions?.Items[1].ImpactDigest);
        using var acceptedReview = await owner.GetAsync(
            $"{boundaryPath}/decisions/{decision.DecisionId}");
        Assert.Equal(HttpStatusCode.OK, acceptedReview.StatusCode);
        using var deniedDecisions = await outsider.GetAsync($"{boundaryPath}/decisions");
        Assert.Equal(HttpStatusCode.NotFound, deniedDecisions.StatusCode);

        using var secondTenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Other boundary tenant",
            slug = $"other-boundary-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, secondTenantResponse.StatusCode);
        var secondTenant = await secondTenantResponse.Content.ReadFromJsonAsync<TenantDocument>();
        Assert.NotNull(secondTenant);
        var secondTenantId = Uuid.Parse(secondTenant.TenantId, CultureInfo.InvariantCulture);
        var administratorsTeamId = BuiltInRbac.AdministratorsTeamId(secondTenantId);
        var secondTenantReady = false;
        string? lastReadinessResponse = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(
                $"/api/v1/tenants/{secondTenant.TenantId}/teams/{administratorsTeamId}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                secondTenantReady = true;
                break;
            }
            lastReadinessResponse = $"{(int)response.StatusCode} " +
                                    await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                lastReadinessResponse);
            await Task.Delay(250);
        }
        Assert.True(secondTenantReady,
            $"The second tenant's administration was not readable: {lastReadinessResponse}");
        using var crossTenantBoundary = await owner.GetAsync(
            $"/api/v1/tenants/{secondTenant.TenantId}/boundaries/{registration.BoundaryId}");
        using var crossTenantList = await owner.GetAsync(
            $"/api/v1/tenants/{secondTenant.TenantId}/programs/{program.ProgramId}/boundaries");
        using var crossTenantVersion = await owner.GetAsync(
            $"/api/v1/tenants/{secondTenant.TenantId}/boundaries/{registration.BoundaryId}/versions/{registration.DraftVersionId}?minimum_boundary_revision=4");
        Assert.Equal(HttpStatusCode.NotFound, crossTenantBoundary.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantVersion.StatusCode);

        var applicationsPath = $"/api/v1/tenants/{tenant.TenantId}/applications";
        ApplicationRegistrationDocument? application = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.PostAsJsonAsync(applicationsPath, new
            {
                name = "Payroll",
                purpose = "Run payroll for the boundary impact preview.",
            });
            if (response.StatusCode == HttpStatusCode.OK)
            {
                application = await response.Content.ReadFromJsonAsync<ApplicationRegistrationDocument>();
                break;
            }
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        Assert.NotNull(application);
        var applicationPath = $"{applicationsPath}/{application.ApplicationId}";
        var applicationProjected = false;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(applicationPath);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                applicationProjected = true;
                break;
            }
            await Task.Delay(250);
        }
        Assert.True(applicationProjected);
        var controlsPath = $"/api/v1/tenants/{tenant.TenantId}/programs/{program.ProgramId}/controls";
        var controlEntryId = Uuid.CreateVersion4();
        using var controlResponse = await owner.PostAsJsonAsync(controlsPath, new
        {
            identifier = "AC-BOUNDARY-IMPACT",
            content = new
            {
                title = "Review payroll access",
                objective = "Ensure payroll access is reviewed.",
                description = "The current Control draft applies to the governed payroll application.",
                implementation_narrative = "The compliance lead reviews the access listing.",
                expected_evidence_descriptions = new List<string> { "Access review record" },
                applicability = new[]
                {
                    new
                    {
                        entry_id = controlEntryId.ToString(),
                        subject_type = "application",
                        subject = "Payroll",
                        governed_record_id = (string?)application.ApplicationId,
                        rationale = "The draft applies to the declared payroll application.",
                        unresolved = false,
                    },
                },
            },
        });
        Assert.Equal(HttpStatusCode.OK, controlResponse.StatusCode);
        var control = await controlResponse.Content.ReadFromJsonAsync<ControlRegistrationDocument>();
        Assert.NotNull(control);
        var applicationScopeEntryId = Uuid.CreateVersion4();
        using var discardedSuccessorResponse = await owner.PostAsJsonAsync(
            $"{boundaryPath}/successors", new
            {
                expected_approved_version_id = registration.DraftVersionId,
                content,
            });
        Assert.Equal(HttpStatusCode.OK, discardedSuccessorResponse.StatusCode);
        var discardedSuccessor = await discardedSuccessorResponse.Content
            .ReadFromJsonAsync<BoundaryRegistrationDocument>();
        Assert.NotNull(discardedSuccessor);
        using var discarded = await owner.PostAsJsonAsync(
            $"{boundaryPath}/drafts/{discardedSuccessor.DraftVersionId}/discards", new
            {
                expected_revision = 1,
                rationale = "This draft was opened by mistake.",
            });
        Assert.Equal(HttpStatusCode.NoContent, discarded.StatusCode);

        using var successorResponse = await owner.PostAsJsonAsync($"{boundaryPath}/successors", new
        {
            expected_approved_version_id = registration.DraftVersionId,
            content = new
            {
                statement = "Service A, provider B, and payroll are in scope.",
                content.engagement_stage,
                content.trust_services_categories,
                entries = new[]
                {
                    new
                    {
                        entry_id = entryId.ToString(),
                        kind = "inclusion",
                        subject_type = "service",
                        subject = "Service A",
                        governed_record_id = (string?)null,
                        owner_reference = "Compliance lead",
                        rationale = "It processes customer data.",
                        unresolved = true,
                    },
                    new
                    {
                        entry_id = applicationScopeEntryId.ToString(),
                        kind = "inclusion",
                        subject_type = "application",
                        subject = "Payroll",
                        governed_record_id = (string?)application.ApplicationId,
                        owner_reference = "Finance",
                        rationale = "Payroll is in scope for the successor boundary.",
                        unresolved = false,
                    },
                },
            },
        });
        Assert.Equal(HttpStatusCode.OK, successorResponse.StatusCode);
        var successor = await successorResponse.Content
            .ReadFromJsonAsync<BoundaryRegistrationDocument>();
        Assert.NotNull(successor);
        var successorDraft = $"{boundaryPath}/drafts/{successor.DraftVersionId}";
        BoundaryImpactPreviewDocument? successorPreview = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(
                $"{successorDraft}/impact-preview?expected_revision=1");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                successorPreview = await response.Content
                    .ReadFromJsonAsync<BoundaryImpactPreviewDocument>();
                break;
            }
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        Assert.NotNull(successorPreview);
        Assert.False(successorPreview.Complete);
        Assert.Contains("controls", successorPreview.PendingContexts);
        Assert.Contains("evidence", successorPreview.PendingContexts);
        var controlsContribution = Assert.Single(successorPreview.Contributions,
            contribution => contribution.Context == "controls");
        Assert.False(controlsContribution.Complete);
        Assert.Contains(controlsContribution.Records, record =>
            record.RecordType == "control_draft" && record.RecordId == control.ControlId);
        Assert.Contains(successorPreview.Contributions.SelectMany(item => item.Records),
            record => record.RecordType == "program" && record.RecordId == program.ProgramId);
        using var successorReview = await reviewer.PostAsJsonAsync($"{successorDraft}/reviews", new
        {
            expected_revision = 1,
            outcome = "accept",
            rationale = "The added provider is described.",
        });
        Assert.Equal(HttpStatusCode.NoContent, successorReview.StatusCode);
        BoundaryDecisionDocument? successorDecision = null;
        deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(boundaryPath);
            var view = await response.Content.ReadFromJsonAsync<BoundaryDocument>();
            successorDecision = view?.LatestDecision;
            if (successorDecision?.Outcome == "accept" &&
                view?.Draft?.VersionId == successor.DraftVersionId)
                break;
            await Task.Delay(250);
        }
        Assert.NotNull(successorDecision);
        using var reviewedDiscard = await owner.PostAsJsonAsync(
            $"{successorDraft}/discards", new
            {
                expected_revision = 1,
                rationale = "A reviewed version must remain in history.",
            });
        Assert.Equal(HttpStatusCode.Conflict, reviewedDiscard.StatusCode);
        using var incompleteApproval = await reviewer.PostAsJsonAsync($"{successorDraft}/approvals", new
        {
            expected_revision = 1,
            accepted_review_decision_id = successorDecision.DecisionId,
            effective_from = "2027-02-01",
            rationale = "Approval must wait for complete impact coverage.",
            impact_digest = successorPreview.Digest,
        });
        Assert.Equal(HttpStatusCode.Conflict, incompleteApproval.StatusCode);
    }

    [Fact]
    public async Task ShouldRecoverApprovedHistoryReadGivenFitzProjectionLag()
    {
        // Arrange
        await using var factory = E2EAppFactory.Create(broker);
        using var scope = factory.Services.CreateScope();
        var directory = Assert.IsType<FitzBoundaryDirectory>(scope.ServiceProvider
            .GetRequiredService<IBoundaryDirectoryReader>());
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var authorId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var reviewId = Uuid.CreateVersion4();
        var approvalId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var content = new BoundaryContent("System boundary", "readiness", ["security"], []);
        var source = new SystemBoundary(tenantId, boundaryId);
        Assert.True(source.Create(programId, versionId, content, authorId,
            "Author", now).IsSuccess);
        Assert.True(source.Review(versionId, 1, reviewId, "accept", "Reviewed",
            reviewerId, "Reviewer", now.AddMinutes(1)).IsSuccess);
        Assert.True(source.Approve(versionId, 1, approvalId, reviewId,
            new DateOnly(2027, 1, 1), "Approved", "digest", reviewerId,
            "Reviewer", now.AddMinutes(2)).IsSuccess);
        var identity = new CheckpointIdentity("BoundaryDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new BoundaryDraftCreated(tenantId, boundaryId,
                programId, versionId, content, authorId, "Author", now));
            await directory.ApplyAsync(new BoundaryReviewed(tenantId, boundaryId,
                versionId, 1, reviewId, "accept", reviewerId, "Reviewer", "Reviewed",
                now.AddMinutes(1)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var handler = new GetBoundaryVersionHandler(directory,
            new BoundaryHistoryReadConsistency(directory, new SourceReader(source)));
        var request = new RequestContext<GetBoundaryVersion>(new GetBoundaryVersion(
            tenantId, boundaryId, versionId, 3), new ClaimsPrincipal());

        // Act
        var lagged = await handler.HandleAsync(request, CancellationToken.None);
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new BoundaryApproved(tenantId, boundaryId,
                versionId, 1, approvalId, reviewId, reviewerId, "Reviewer", "Approved",
                new DateOnly(2027, 1, 1), now.AddMinutes(2), "digest"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var caughtUp = await handler.HandleAsync(request, CancellationToken.None);

        // Assert
        var lagError = Assert.IsType<RequestError>(lagged.Error);
        Assert.Equal(RequestErrorKind.Conflict, lagError.Kind);
        Assert.Contains("projection", lagError.Message, StringComparison.Ordinal);
        Assert.True(caughtUp.IsSuccess);
        Assert.Equal("approved", caughtUp.Value.Status);
        Assert.Equal(versionId, caughtUp.Value.VersionId);
    }

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }

    sealed record TenantDocument([property: JsonPropertyName("tenant_id")] string TenantId);
    sealed record ProgramDocument([property: JsonPropertyName("program_id")] string ProgramId);
    sealed record BoundaryRegistrationDocument(
        [property: JsonPropertyName("boundary_id")] string BoundaryId,
        [property: JsonPropertyName("draft_version_id")] string DraftVersionId);
    sealed record ApplicationRegistrationDocument(
        [property: JsonPropertyName("application_id")] string ApplicationId);
    sealed record ControlRegistrationDocument(
        [property: JsonPropertyName("control_id")] string ControlId);
    sealed record BoundaryDocument(BoundaryVersionDocument? Draft,
        [property: JsonPropertyName("boundary_id")] string BoundaryId,
        [property: JsonPropertyName("latest_approved_version")] BoundaryVersionDocument? LatestApprovedVersion,
        [property: JsonPropertyName("latest_decision")] BoundaryDecisionDocument? LatestDecision,
        long Revision);
    sealed record BoundaryPageDocument(IReadOnlyList<BoundaryDocument> Items);
    sealed record BoundaryVersionDocument(
        [property: JsonPropertyName("version_id")] string VersionId,
        long Revision, BoundaryContentDocument Content, string Status,
        [property: JsonPropertyName("effective_from")] string? EffectiveFrom);
    sealed record BoundaryContentDocument(string Statement);
    sealed record BoundaryVersionPageDocument(IReadOnlyList<BoundaryVersionDocument> Items);
    sealed record BoundaryDecisionDocument(
        [property: JsonPropertyName("decision_id")] string DecisionId, string Outcome,
        [property: JsonPropertyName("relies_on_decision_id")] string? ReliesOnDecisionId,
        [property: JsonPropertyName("impact_digest")] string? ImpactDigest);
    sealed record BoundaryDecisionPageDocument(IReadOnlyList<BoundaryDecisionDocument> Items);
    sealed record BoundaryImpactPreviewDocument(bool Complete, string Digest,
        IReadOnlyList<BoundaryChangeDocument> Changes,
        IReadOnlyList<BoundaryImpactContributionDocument> Contributions,
        [property: JsonPropertyName("pending_contexts")] IReadOnlyList<string> PendingContexts);
    sealed record BoundaryChangeDocument(string Field,
        [property: JsonPropertyName("change_type")] string ChangeType);
    sealed record BoundaryImpactContributionDocument(string Context, bool Complete,
        IReadOnlyList<BoundaryAffectedRecordDocument> Records);
    sealed record BoundaryAffectedRecordDocument(
        [property: JsonPropertyName("record_type")] string RecordType,
        [property: JsonPropertyName("record_id")] string RecordId);
}

// Boundary lifecycle tests exercise a fresh broker so tenant bootstrap cannot queue behind
// the unrelated tenant histories accumulated by the rest of the broker suite.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BoundaryBrokerCollectionDefinition : ICollectionFixture<BrokerStackFixture>
{
    public const string Name = "Boundary broker e2e";
}
