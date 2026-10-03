using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Portia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Proves control activation with a non-signing-in person owner and template provenance,
///     reviewed criterion coverage and not-applicable decisions through the Fitz coverage
///     projections, control-linked risk treatment with residual assessment, the method-change
///     reassessment reactor, the full control impact preview, and proposal withdrawal against the
///     real broker on a standalone host and on split API and worker hosts.
/// </summary>
[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ControlRiskGovernanceE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    static readonly string[] Scale = ["Rare", "Unlikely", "Possible", "Likely", "Almost certain"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldGovernControlsRisksAndCoverageGivenBrokerHosts(bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-governance-{Guid.NewGuid():N}";
        using var worker = splitHosts ? BuildWorker(applicationName) : null;
        if (worker is not null)
            await worker.StartAsync();
        await using var baseFactory = E2EAppFactory.Create(broker, applicationName);
        await using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("Compliance:Controls:LifecycleEnabled", "true"));
        var priorHostMode = TestHostMode.Current;
        HttpClient ownerClient;
        HttpClient reviewerClient;
        HttpClient outsiderClient;
        try
        {
            TestHostMode.Set(splitHosts ? "api" : "standalone");
            ownerClient = factory.CreateClient();
            reviewerClient = factory.CreateClient();
            outsiderClient = factory.CreateClient();
        }
        finally
        {
            TestHostMode.Set(priorHostMode);
        }
        using var owner = ownerClient;
        using var reviewer = reviewerClient;
        using var outsider = outsiderClient;
        await TenantInvitationE2ETests.LoginAsync(owner,
            $"governance-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"governance-outsider-{Guid.NewGuid():N}@example.com");
        var (tenantId, programId) = await CreateProgramAsync(owner);
        var tenantPath = $"/api/v1/tenants/{tenantId}";
        var programPath = $"{tenantPath}/programs/{programId}";
        var editions = await ReadAsync(await owner.GetAsync($"{tenantPath}/criteria-editions"));
        var editionId = editions.EnumerateArray().First().GetProperty("edition_id").GetString()!;
        using (var selected = await RetryUntilAuthorizedAsync(() => owner.PutAsJsonAsync(
                   $"{programPath}/criteria-edition",
                   new { expected_revision = 1, edition_id = editionId })))
            Assert.Equal(HttpStatusCode.NoContent, selected.StatusCode);
        await InviteReviewerAsync(factory, worker, owner, reviewer, tenantId);
        using var personResponse = await RetryUntilAuthorizedAsync(() => owner.PostAsJsonAsync(
            $"{tenantPath}/people", new { display_name = "Facilities manager" }));
        Assert.Equal(HttpStatusCode.OK, personResponse.StatusCode);
        var personId = (await ReadAsync(personResponse)).GetProperty("person_id").GetString()!;

        // Act: activate a template control owned by a person who does not sign in.
        using var controlResponse = await owner.PostAsJsonAsync($"{programPath}/controls", new
        {
            identifier = "AC-GOV",
            content = Content("Badge access review", new
            {
                origin = "template",
                source_name = "Starter pack",
                source_reference = "AC-1",
            }),
        });
        Assert.Equal(HttpStatusCode.OK, controlResponse.StatusCode);
        var controlId = (await ReadAsync(controlResponse)).GetProperty("control_id").GetString()!;
        var controlPath = $"{programPath}/controls/{controlId}";
        using (var designated = await owner.PutAsJsonAsync($"{controlPath}/draft/owner-person",
                   new
                   {
                       expected_revision = 1,
                       person_id = personId,
                       rationale = "The facilities manager owns badge access.",
                   }))
            Assert.Equal(HttpStatusCode.NoContent, designated.StatusCode);
        using (var selfReview = await owner.PostAsJsonAsync($"{controlPath}/draft/reviews",
                   new { expected_revision = 1, outcome = "accept", rationale = "Mine." }))
            Assert.Equal(HttpStatusCode.Forbidden, selfReview.StatusCode);
        using (var reviewed = await RetryUntilAuthorizedAsync(() => reviewer.PostAsJsonAsync(
                   $"{controlPath}/draft/reviews",
                   new { expected_revision = 1, outcome = "accept", rationale = "Complete." })))
            Assert.Equal(HttpStatusCode.NoContent, reviewed.StatusCode);
        var reviewId = (await ReadAsync(await owner.GetAsync($"{controlPath}/decisions")))
            .GetProperty("items")[0].GetProperty("decision_id").GetString()!;
        using (var approved = await reviewer.PostAsJsonAsync($"{controlPath}/draft/approvals",
                   new
                   {
                       expected_revision = 1,
                       accepted_review_decision_id = reviewId,
                       effective_from = "2026-10-01",
                       rationale = "Ready to operate.",
                   }))
            Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);
        var version = await ReadAsync(await owner.GetAsync($"{controlPath}/current-version"));
        var versionId = version.GetProperty("version_id").GetString()!;

        // Act: map one criterion and record another as not applicable, each reviewed.
        using var mappingResponse = await owner.PostAsJsonAsync($"{programPath}/control-mappings",
            new
            {
                control_id = controlId,
                control_version_id = versionId,
                edition_id = editionId,
                criterion_identifier = "CC6.1",
                expected_revision = 0,
                rationale = "Badge reviews restrict physical access.",
                applicability_explanation = "Applies to every office.",
            });
        Assert.Equal(HttpStatusCode.OK, mappingResponse.StatusCode);
        var mappingId = (await ReadAsync(mappingResponse)).GetProperty("mapping_id").GetString()!;
        using (var mappingReview = await reviewer.PostAsJsonAsync(
                   $"{programPath}/control-mappings/{mappingId}/reviews",
                   new { expected_revision = 1, outcome = "accept", rationale = "Supported." }))
            Assert.Equal(HttpStatusCode.NoContent, mappingReview.StatusCode);
        using var notApplicable = await owner.PostAsJsonAsync(
            $"{programPath}/criterion-applicability", new
            {
                edition_id = editionId,
                criterion_identifier = "CC1.1",
                expected_revision = 0,
                rationale = "The criterion is carved out of this engagement.",
            });
        Assert.Equal(HttpStatusCode.OK, notApplicable.StatusCode);
        var decisionId = (await ReadAsync(notApplicable)).GetProperty("decision_id").GetString()!;
        using (var selfDecision = await owner.PostAsJsonAsync(
                   $"{programPath}/criterion-applicability/{decisionId}/reviews",
                   new { expected_revision = 1, outcome = "accept", rationale = "Mine." }))
            Assert.Equal(HttpStatusCode.Forbidden, selfDecision.StatusCode);
        using (var decisionReview = await reviewer.PostAsJsonAsync(
                   $"{programPath}/criterion-applicability/{decisionId}/reviews",
                   new { expected_revision = 1, outcome = "accept", rationale = "Agreed." }))
            Assert.Equal(HttpStatusCode.NoContent, decisionReview.StatusCode);
        var coverage = await WaitForCoverageAsync(owner,
            $"{programPath}/criteria-coverage?kind=criterion&limit=200");
        var decisions = await WaitForOkAsync(owner, $"{programPath}/criterion-applicability");

        // Act: treat a risk with the control and record its residual assessment.
        using var riskResponse = await owner.PostAsJsonAsync($"{programPath}/risks", new
        {
            identifier = "R-GOV",
            title = "Unauthorized office entry",
            scenario = "A former employee keeps a working badge",
            potential_effect = "Physical access to equipment",
        });
        Assert.Equal(HttpStatusCode.OK, riskResponse.StatusCode);
        var riskId = (await ReadAsync(riskResponse)).GetProperty("risk_id").GetString()!;
        var riskPath = $"{programPath}/risks/{riskId}";
        await PublishMethodAsync(owner, programPath, 0);
        using (var inherent = await owner.PostAsJsonAsync($"{riskPath}/assessments", Assessment(
                   0, "inherent", 4)))
            Assert.Equal(HttpStatusCode.OK, inherent.StatusCode);
        using (var treatment = await owner.PutAsJsonAsync($"{riskPath}/treatment", new
        {
            expected_revision = 1,
            kind = "mitigate",
            rationale = "Quarterly badge reviews.",
        }))
            Assert.Equal(HttpStatusCode.NoContent, treatment.StatusCode);
        using var earlyResidual = await owner.PostAsJsonAsync($"{riskPath}/assessments",
            Assessment(2, "residual", 2));
        using var treatmentResponse = await owner.PostAsJsonAsync($"{riskPath}/control-treatments",
            new
            {
                expected_revision = 0,
                control_id = controlId,
                control_version_id = versionId,
                rationale = "The badge review removes stale access.",
            });
        Assert.Equal(HttpStatusCode.OK, treatmentResponse.StatusCode);
        var treatmentId = (await ReadAsync(treatmentResponse)).GetProperty("treatment_id")
            .GetString()!;
        using var selfTreatmentReview = await owner.PostAsJsonAsync(
            $"{riskPath}/control-treatments/{treatmentId}/reviews",
            new { expected_revision = 1, outcome = "accept", rationale = "Mine." });
        using (var treatmentReview = await reviewer.PostAsJsonAsync(
                   $"{riskPath}/control-treatments/{treatmentId}/reviews",
                   new { expected_revision = 1, outcome = "accept", rationale = "Supported." }))
            Assert.Equal(HttpStatusCode.NoContent, treatmentReview.StatusCode);
        using var residual = await owner.PostAsJsonAsync($"{riskPath}/assessments",
            Assessment(2, "residual", 2));

        // Act: a later method version raises a durable reassessment trigger through the reactor.
        await PublishMethodAsync(owner, programPath, 1);
        var governance = await WaitForTriggerAsync(owner, $"{riskPath}/governance");
        var evaluation = await WaitForOkAsync(owner, $"{riskPath}/evaluation");

        // Act: propose a successor, preview its full impact, then withdraw it.
        using var successorResponse = await owner.PostAsJsonAsync($"{controlPath}/successors",
            new
            {
                expected_approved_version_id = versionId,
                content = Content("Badge access review v2", null),
            });
        Assert.Equal(HttpStatusCode.OK, successorResponse.StatusCode);
        var successorRevision = (await ReadAsync(successorResponse)).GetProperty("revision")
            .GetInt64();
        var preview = await ReadAsync(await owner.GetAsync(
            $"{controlPath}/impact-preview?expected_revision={successorRevision}"));
        using var withdrawn = await owner.PostAsJsonAsync($"{controlPath}/proposal-withdrawals",
            new { expected_revision = successorRevision, rationale = "Deferred to next year." });
        var restored = await WaitForDraftRevisionAsync(owner, $"{controlPath}/draft",
            successorRevision + 1);
        var controlDecisions = await ReadAsync(await owner.GetAsync($"{controlPath}/decisions"));
        using var outsiderGovernance = await outsider.GetAsync($"{riskPath}/governance");
        using var outsiderCoverage = await outsider.GetAsync($"{programPath}/criteria-coverage");

        // Assert
        Assert.Equal("verified_person", version.GetProperty("owner_resolution").GetString());
        Assert.Equal(personId, version.GetProperty("owner_person_id").GetString());
        Assert.Equal("template", version.GetProperty("content_origin").GetString());
        Assert.Equal("Starter pack", version.GetProperty("content").GetProperty("provenance")
            .GetProperty("source_name").GetString());
        var items = coverage.GetProperty("items").EnumerateArray().ToArray();
        var mapped = items.Single(item => item.GetProperty("identifier").GetString() == "CC6.1");
        Assert.Equal("mapped", mapped.GetProperty("coverage_state").GetString());
        Assert.False(mapped.GetProperty("mapped_controls")[0].GetProperty("remap_required")
            .GetBoolean());
        var excluded = items.Single(item => item.GetProperty("identifier").GetString() == "CC1.1");
        Assert.Equal("not_applicable", excluded.GetProperty("coverage_state").GetString());
        Assert.Equal(decisionId, excluded.GetProperty("not_applicable_decision_id").GetString());
        Assert.Equal("not_applicable", decisions.GetProperty("items")[0].GetProperty("status")
            .GetString());
        Assert.Equal(HttpStatusCode.Conflict, earlyResidual.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, selfTreatmentReview.StatusCode);
        Assert.Equal(HttpStatusCode.OK, residual.StatusCode);
        Assert.Equal(4, (await ReadAsync(residual)).GetProperty("score").GetInt32());
        Assert.Equal("accepted", governance.GetProperty("control_treatments")[0]
            .GetProperty("status").GetString());
        var trigger = governance.GetProperty("reassessment_triggers")[0];
        Assert.Equal("method_changed", trigger.GetProperty("trigger_kind").GetString());
        Assert.Equal("open", trigger.GetProperty("status").GetString());
        Assert.Equal("reassessment_due", evaluation.GetProperty("status").GetString());
        var contributions = preview.GetProperty("contributions").EnumerateArray()
            .ToDictionary(item => item.GetProperty("context").GetString()!);
        Assert.Equal(mappingId, contributions["mappings"].GetProperty("records")[0]
            .GetProperty("record_id").GetString());
        Assert.Equal(treatmentId, contributions["risk_treatments"].GetProperty("records")[0]
            .GetProperty("record_id").GetString());
        Assert.Equal("complete", contributions["readiness"].GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);
        Assert.Equal("Badge access review", restored.GetProperty("content").GetProperty("title")
            .GetString());
        Assert.Equal(versionId, restored.GetProperty("draft_version_id").GetString());
        Assert.Equal("withdrawal", controlDecisions.GetProperty("items").EnumerateArray().Last()
            .GetProperty("kind").GetString());
        Assert.Equal(HttpStatusCode.NotFound, outsiderGovernance.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderCoverage.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldCancelUnfinishedRiskActionsWithoutErasingHistoryOrCompletingTreatmentGivenStandaloneOrSplitHost(
        bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-risk-cancellation-{Guid.NewGuid():N}";
        using var worker = splitHosts ? BuildWorker(applicationName) : null;
        if (worker is not null)
            await worker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        var previousMode = TestHostMode.Current;
        HttpClient ownerClient;
        HttpClient outsiderClient;
        try
        {
            TestHostMode.Set(splitHosts ? "api" : "standalone");
            ownerClient = factory.CreateClient();
            outsiderClient = factory.CreateClient();
        }
        finally
        {
            TestHostMode.Set(previousMode);
        }
        using var owner = ownerClient;
        using var outsider = outsiderClient;
        var ownerUserId = await TenantInvitationE2ETests.LoginAsync(owner,
            $"risk-cancellation-owner-{Guid.NewGuid():N}@example.com");
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"risk-cancellation-outsider-{Guid.NewGuid():N}@example.com");
        var (tenantId, programId) = await CreateProgramAsync(owner);
        var tenantUuid = Uuid.Parse(tenantId.ToString(), CultureInfo.InvariantCulture);
        var ownerUuid = Uuid.Parse(ownerUserId, CultureInfo.InvariantCulture);
        var ownerMemberId = RbacIds.Member(tenantUuid, ownerUuid);
        var programPath = $"/api/v1/tenants/{tenantId}/programs/{programId}";
        using var createdRisk = await RetryUntilAuthorizedAsync(() => owner.PostAsJsonAsync(
            $"{programPath}/risks", new
            {
                identifier = "R-CANCEL",
                title = "Provider control becomes unavailable",
                scenario = "The provider stops applying the agreed safeguard",
                potential_effect = "Customer data may be exposed",
            }));
        Assert.Equal(HttpStatusCode.OK, createdRisk.StatusCode);
        var riskId = (await ReadAsync(createdRisk)).GetProperty("risk_id").GetString()!;
        var riskPath = $"{programPath}/risks/{riskId}";
        await PublishMethodAsync(owner, programPath, 0);
        using (var assessed = await owner.PostAsJsonAsync($"{riskPath}/assessments",
                   Assessment(0, "inherent", 4)))
            Assert.Equal(HttpStatusCode.OK, assessed.StatusCode);
        using var treatment = await owner.PutAsJsonAsync($"{riskPath}/treatment", new
        {
            expected_revision = 1,
            kind = "mitigate",
            rationale = "Maintain the safeguard through assigned work.",
        });
        Assert.Equal(HttpStatusCode.NoContent, treatment.StatusCode);

        var actionPath = $"{riskPath}/treatment-actions";
        using var firstAction = await owner.PostAsJsonAsync(actionPath, new
        {
            expected_revision = 0,
            title = "Confirm provider safeguards",
            target_state = "Provider control is operating",
            expected_evidence = "A current review record",
            due_on = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5),
            accountable_member_id = ownerMemberId,
        });
        Assert.Equal(HttpStatusCode.OK, firstAction.StatusCode);
        var firstActionId = (await ReadAsync(firstAction)).GetProperty("action_id").GetString()!;
        using var secondAction = await owner.PostAsJsonAsync(actionPath, new
        {
            expected_revision = 1,
            title = "Validate provider recovery",
            target_state = "Recovery procedure is confirmed",
            expected_evidence = "A tested recovery record",
            due_on = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(8),
            accountable_member_id = ownerMemberId,
        });
        Assert.Equal(HttpStatusCode.OK, secondAction.StatusCode);
        var secondActionId = (await ReadAsync(secondAction)).GetProperty("action_id").GetString()!;
        var workPath = $"{programPath}/work";
        _ = await WaitForRiskWorkItemsAsync(owner, workPath, firstActionId, secondActionId);

        // Act
        var cancellationPath = $"{actionPath}/{firstActionId}/cancellations";
        using var stale = await owner.PostAsJsonAsync(cancellationPath, new
        {
            expected_revision = 0,
            rationale = "This uses an out-of-date governance revision.",
        });
        var (otherTenantId, otherProgramId) = await CreateProgramAsync(owner);
        using var otherTenantRead = await owner.GetAsync(
            $"/api/v1/tenants/{otherTenantId}/programs/{otherProgramId}/risks/{riskId}/governance");
        using var foreignCancellation = await owner.PostAsJsonAsync(
            $"/api/v1/tenants/{otherTenantId}/programs/{otherProgramId}/risks/{riskId}/" +
            $"treatment-actions/{firstActionId}/cancellations", new
            {
                expected_revision = 2,
                rationale = "Foreign tenant must not see this action.",
            });
        using var outsiderCancellation = await outsider.PostAsJsonAsync(cancellationPath,
            new { expected_revision = 2, rationale = "Unauthorized cancellation." });
        using var cancelledFirst = await owner.PostAsJsonAsync(cancellationPath, new
        {
            expected_revision = 2,
            rationale = "The provider relationship is ending.",
        });
        using var cancelledSecond = await owner.PostAsJsonAsync(
            $"{actionPath}/{secondActionId}/cancellations", new
            {
                expected_revision = 3,
                rationale = "The dependent recovery work is no longer required.",
            });
        var governance = await WaitForGovernanceAsync(owner, $"{riskPath}/governance", 4);
        var work = await WaitForRiskWorkItemsAsync(owner, workPath);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherTenantRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignCancellation.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsiderCancellation.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancelledFirst.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancelledSecond.StatusCode);
        Assert.DoesNotContain(work.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("kind").GetString() == "risk_treatment_action");
        Assert.Equal("none", governance.GetProperty("treatment_action_status").GetString());
        Assert.Equal(2, governance.GetProperty("treatment_actions").GetArrayLength());
        foreach (var action in governance.GetProperty("treatment_actions").EnumerateArray())
        {
            Assert.Equal("cancelled", action.GetProperty("status").GetString());
            Assert.False(action.GetProperty("overdue").GetBoolean());
            Assert.NotEmpty(action.GetProperty("cancellation_rationale").GetString()!);
            Assert.Equal(ownerMemberId.ToString(), action.GetProperty("cancelled_by")
                .GetProperty("id").GetString());
            Assert.Empty(action.GetProperty("completions").EnumerateArray());
        }
    }

    static object Content(string title, object? provenance) => new
    {
        title,
        objective = "Restrict office access to current staff",
        description = "Management reviews badge access",
        implementation_narrative = "The office manager reviews active badges quarterly.",
        expected_evidence_descriptions = new[] { "Dated badge review record" },
        provenance,
    };

    static object Assessment(long expectedRevision, string phase, int rating) => new
    {
        expected_revision = expectedRevision,
        method_version = 1,
        phase,
        likelihood = rating,
        impact = rating,
        rationale = "Assessed against the qualitative scale.",
    };

    static async Task PublishMethodAsync(HttpClient owner, string programPath,
        long expectedVersion)
    {
        using var published = await owner.PostAsJsonAsync($"{programPath}/risk-method/versions",
            new
            {
                expected_version = expectedVersion,
                likelihood_scale = Scale,
                impact_scale = Scale,
                appetite_threshold = 12,
            });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
    }

    static async Task InviteReviewerAsync(WebApplicationFactory<Program> factory, IHost? worker,
        HttpClient owner, HttpClient reviewer, Guid tenantId)
    {
        var reviewerEmail = $"governance-reviewer-{Guid.NewGuid():N}@example.com";
        using var invitation = await owner.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/invitations",
            new { email_address = reviewerEmail, affiliation = "client_personnel", administrator = false });
        Assert.Equal(HttpStatusCode.NoContent, invitation.StatusCode);
        var delivery = worker is not null
            ? worker.Services.GetRequiredService<MockTenantInvitationDelivery>()
            : factory.Services.GetRequiredService<MockTenantInvitationDelivery>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        string? token = null;
        while (DateTimeOffset.UtcNow < deadline &&
               !delivery.TryGetLatest(Uuid.Parse(tenantId.ToString(), CultureInfo.InvariantCulture),
                   reviewerEmail, out token))
            await Task.Delay(250);
        Assert.NotNull(token);
        var reviewerId = await TenantInvitationE2ETests.LoginAsync(reviewer, reviewerEmail);
        await TenantInvitationE2ETests.VerifyEmailAsync(factory, reviewer, reviewerId,
            reviewerEmail, worker?.Services.GetRequiredService<MockEmailChallengeDelivery>());
        using var accepted = await reviewer.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/invitations/acceptance",
            new { email_address = reviewerEmail, token });
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        var tenant = Uuid.Parse(tenantId.ToString(), CultureInfo.InvariantCulture);
        var reviewerMemberId = RbacIds.Member(tenant,
            Uuid.Parse(reviewerId, CultureInfo.InvariantCulture));
        using var assigned = await RetryUntilAuthorizedAsync(() => owner.PostAsync(
            $"/api/v1/tenants/{tenantId}/teams/" +
            $"{BuiltInRbac.PowerUsersTeamId(tenant)}/members/{reviewerMemberId}", null));
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        await AccessGrantE2ESupport.IssuePowerUserTeamOrganizationGrantAsync(owner, tenant);
    }

    static async Task<HttpResponseMessage> RetryUntilAuthorizedAsync(
        Func<Task<HttpResponseMessage>> send)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (true)
        {
            var response = await send();
            if (response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Forbidden) ||
                DateTimeOffset.UtcNow >= deadline)
                return response;
            response.Dispose();
            await Task.Delay(250);
        }
    }

    static async Task<JsonElement> WaitForCoverageAsync(HttpClient client, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var page = await ReadAsync(response);
                var states = page.GetProperty("items").EnumerateArray().ToDictionary(
                    item => item.GetProperty("identifier").GetString()!,
                    item => item.GetProperty("coverage_state").GetString());
                if (states.GetValueOrDefault("CC6.1") == "mapped" &&
                    states.GetValueOrDefault("CC1.1") == "not_applicable")
                    return page;
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                Assert.Equal("true", response.Headers.GetValues("Portia-Transient").Single());
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("The coverage projections did not catch up.");
    }

    static async Task<JsonElement> WaitForTriggerAsync(HttpClient client, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var governance = await ReadAsync(response);
            if (governance.GetProperty("reassessment_triggers").GetArrayLength() > 0)
                return governance;
            await Task.Delay(250);
        }
        throw new TimeoutException("The method reassessment reactor did not raise a trigger.");
    }

    static async Task<JsonElement> WaitForOkAsync(HttpClient client, string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
                return await ReadAsync(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        throw new TimeoutException($"The projection behind {path} did not catch up.");
    }

    static async Task<JsonElement> WaitForDraftRevisionAsync(HttpClient client, string path,
        long revision)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"{path}?minimum_revision={revision}");
            if (response.StatusCode == HttpStatusCode.OK)
                return await ReadAsync(response);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(250);
        }
        throw new TimeoutException("The control draft projection did not catch up.");
    }

    static async Task<JsonElement> WaitForRiskWorkItemsAsync(HttpClient client, string path,
        params string[] expectedActionIds)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var queue = await ReadAsync(response);
                var actionIds = queue.GetProperty("items").EnumerateArray()
                    .Where(item => item.GetProperty("kind").GetString() == "risk_treatment_action")
                    .Select(item => item.GetProperty("source_id").GetString()).ToArray();
                if (expectedActionIds.Length == 0
                    ? actionIds.Length == 0
                    : expectedActionIds.All(expected => actionIds.Contains(expected)))
                    return queue;
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("The risk treatment actions did not reach the work queue.");
    }

    static async Task<JsonElement> WaitForGovernanceAsync(HttpClient client, string path,
        long revision)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var governance = await ReadAsync(response);
                if (governance.GetProperty("revision").GetInt64() >= revision)
                    return governance;
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("The cancelled risk treatment actions were not projected.");
    }

    static async Task<(Guid TenantId, Guid ProgramId)> CreateProgramAsync(HttpClient owner)
    {
        using var tenant = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Governance tenant",
            slug = $"governance-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenant.StatusCode);
        var tenantId = Guid.Parse((await ReadAsync(tenant)).GetProperty("tenant_id").GetString()!);
        await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(owner, tenantId);
        var path = $"/api/v1/tenants/{tenantId}/programs";
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var program = await owner.PostAsJsonAsync(path, new
            {
                name = "SOC 2",
                plan = new
                {
                    target_readiness_date = (string?)null,
                    target_type_i_as_of_date = (string?)null,
                    target_type_ii_start_date = (string?)null,
                    target_type_ii_end_date = (string?)null,
                    readiness_advisor = (string?)null,
                    audit_firm = (string?)null,
                },
            });
            if (program.StatusCode == HttpStatusCode.OK)
                return (tenantId, Guid.Parse((await ReadAsync(program))
                    .GetProperty("program_id").GetString()!));
            Assert.True(program.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                await program.Content.ReadAsStringAsync());
            await Task.Delay(250);
        }
        throw new TimeoutException("Program creation never became authorized after tenant bootstrap.");
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

    static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
        .RootElement.Clone();
}
