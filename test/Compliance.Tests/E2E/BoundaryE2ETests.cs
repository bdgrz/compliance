using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class BoundaryE2ETests(BrokerStackFixture broker)
{
    [Fact]
    public async Task ShouldProjectTenantScopedDraftGivenBoundaryCreationAndRevision()
    {
        await using var factory = E2EAppFactory.Create(broker);
        using var owner = factory.CreateClient();
        using var outsider = factory.CreateClient();
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"boundary-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"boundary-outsider-{Guid.NewGuid():N}@example.com");

        using var tenantResponse = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Boundary E2E",
            slug = $"boundary-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenantResponse.StatusCode);
        var tenant = await tenantResponse.Content.ReadFromJsonAsync<TenantDocument>();
        Assert.NotNull(tenant);
        var programsPath = $"/api/v1/tenants/{tenant.TenantId}/programs";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        ProgramDocument? program = null;
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
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await response.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
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
        Assert.Equal("Service A handles customer work.", projected?.Draft?.Content.Statement);
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
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(boundaryPath);
            projected = await response.Content.ReadFromJsonAsync<BoundaryDocument>();
            if (projected?.Draft?.Revision == 2)
                break;
            await Task.Delay(250);
        }
        Assert.Equal(2, projected?.Draft?.Revision);
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
            $"{boundaryPath}/versions/{registration.DraftVersionId}");
        Assert.Equal(HttpStatusCode.OK, versionResponse.StatusCode);
        var version = await versionResponse.Content.ReadFromJsonAsync<BoundaryVersionDocument>();
        Assert.Equal("approved", version?.Status);
        Assert.Equal("2027-01-01", version?.EffectiveFrom);
        Assert.Equal("Service A and its provider are in scope.", version?.Content.Statement);
        using var versionsResponse = await owner.GetAsync($"{boundaryPath}/versions");
        Assert.Equal(HttpStatusCode.OK, versionsResponse.StatusCode);
        var versions = await versionsResponse.Content.ReadFromJsonAsync<BoundaryVersionPageDocument>();
        Assert.Equal(registration.DraftVersionId, Assert.Single(versions?.Items ?? []).VersionId);
        using var effective = await owner.GetAsync(
            $"{boundaryPath}/effective-version?effective_on=2027-01-01");
        Assert.True(effective.StatusCode == HttpStatusCode.OK,
            await effective.Content.ReadAsStringAsync());
        var effectiveVersion = await effective.Content.ReadFromJsonAsync<BoundaryVersionDocument>();
        Assert.Equal(registration.DraftVersionId, effectiveVersion?.VersionId);
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
        deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await owner.GetAsync(
                $"/api/v1/tenants/{secondTenant.TenantId}");
            if (response.StatusCode == HttpStatusCode.OK)
                break;
            await Task.Delay(250);
        }
        using var crossTenantBoundary = await owner.GetAsync(
            $"/api/v1/tenants/{secondTenant.TenantId}/boundaries/{registration.BoundaryId}");
        using var crossTenantList = await owner.GetAsync(
            $"/api/v1/tenants/{secondTenant.TenantId}/programs/{program.ProgramId}/boundaries");
        using var crossTenantVersion = await owner.GetAsync(
            $"/api/v1/tenants/{secondTenant.TenantId}/boundaries/{registration.BoundaryId}/versions/{registration.DraftVersionId}");
        Assert.Equal(HttpStatusCode.NotFound, crossTenantBoundary.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantVersion.StatusCode);

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
                statement = "Service A and provider B are in scope.",
                content.engagement_stage,
                content.trust_services_categories,
                content.entries,
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

    sealed record TenantDocument([property: JsonPropertyName("tenant_id")] string TenantId);
    sealed record ProgramDocument([property: JsonPropertyName("program_id")] string ProgramId);
    sealed record BoundaryRegistrationDocument(
        [property: JsonPropertyName("boundary_id")] string BoundaryId,
        [property: JsonPropertyName("draft_version_id")] string DraftVersionId);
    sealed record BoundaryDocument(BoundaryVersionDocument? Draft,
        [property: JsonPropertyName("boundary_id")] string BoundaryId,
        [property: JsonPropertyName("latest_approved_version")] BoundaryVersionDocument? LatestApprovedVersion,
        [property: JsonPropertyName("latest_decision")] BoundaryDecisionDocument? LatestDecision);
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
    sealed record BoundaryImpactContributionDocument(IReadOnlyList<BoundaryAffectedRecordDocument> Records);
    sealed record BoundaryAffectedRecordDocument(
        [property: JsonPropertyName("record_type")] string RecordType,
        [property: JsonPropertyName("record_id")] string RecordId);
}
