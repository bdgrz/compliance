using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     Drives R2-01, R2-04, and R2-07 over HTTP against the broker: blockers, plan proposal and
///     independent approval, a failed attestation, and the reactor that converts it into a
///     finding, in both standalone and split API/worker hosts.
/// </summary>
[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class ControlOperationE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRouteFailedAttestationToFindingGivenApprovedOperatingPlan(
        bool splitHosts)
    {
        // Arrange
        var applicationName = $"compliance-operations-{Guid.NewGuid():N}";
        using var worker = splitHosts ? BuildWorker(applicationName) : null;
        if (worker is not null)
            await worker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        var (ownerClient, outsiderClient) = CreateClients(factory, splitHosts);
        using var owner = ownerClient;
        using var outsider = outsiderClient;
        var ownerUserId = Uuid.Parse(await TenantInvitationE2ETests.LoginAsync(owner,
            $"operations-owner-{Guid.NewGuid():N}@example.com"), CultureInfo.InvariantCulture);
        await TenantInvitationE2ETests.LoginAsync(outsider,
            $"operations-outsider-{Guid.NewGuid():N}@example.com");
        var (tenantId, programId) = await CreateProgramAsync(owner);
        var tenant = Uuid.Parse(tenantId.ToString(), CultureInfo.InvariantCulture);
        var program = Uuid.Parse(programId.ToString(), CultureInfo.InvariantCulture);
        var ownerMemberId = RbacIds.Member(tenant, ownerUserId);
        var reviewerUserId = Uuid.CreateVersion4();
        var reviewerMemberId = RbacIds.Member(tenant, reviewerUserId);
        var controlId = ControlDraft.IdFor(tenant, program, "AC-E2E");
        var effectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-60);
        await SeedAsync(factory, new Member(tenant, reviewerUserId), member => member.Register());
        await SeedApprovedControlAsync(factory, tenant, program, controlId, ownerMemberId,
            effectiveFrom);
        var programPath = $"/api/v1/tenants/{tenantId}/programs/{programId}";
        var controlPath = $"{programPath}/controls/{controlId}";
        var blocked = await WaitForAsync(owner, $"{programPath}/operating-blockers",
            page => page.GetProperty("items").EnumerateArray().Any(item =>
                item.GetProperty("kind").GetString() == "missing_plan"));

        // Act
        using var proposal = await owner.PostAsJsonAsync($"{controlPath}/operating-plan/proposals",
            new
            {
                expected_revision = 0,
                control_version_id = ControlVersionIds.Initial(controlId).ToString(),
                owner = new { kind = "member", id = ownerMemberId.ToString() },
                reviewer_member_id = reviewerMemberId.ToString(),
                cadence = new
                {
                    kind = "recurring",
                    frequency = "monthly",
                    first_period_start = effectiveFrom,
                    due_within_days = 5,
                },
                effective_from = effectiveFrom,
                rationale = "Owner runs the access review monthly.",
            });
        var proposed = await ReadAsync(proposal);
        var planVersionId = proposed.GetProperty("plan_version_id").GetString()!;
        using var selfApproval = await owner.PostAsJsonAsync(
            $"{controlPath}/operating-plan/proposals/{planVersionId}/approvals",
            new { expected_revision = 1, rationale = "Mine." });
        await SeedAsync(factory, new ControlOperationsLedger(tenant, program), ledger =>
            Command(ledger.ApprovePlan(controlId, 1, Uuid.Parse(planVersionId,
                    CultureInfo.InvariantCulture), "Independent approval.", Uuid.CreateVersion4(),
                "Approver", DateTimeOffset.UtcNow, null)));
        using var missedResponse = await owner.GetAsync($"{controlPath}/occurrences?state=missed");
        var missed = (await ReadAsync(missedResponse)).GetProperty("items")[0];
        var occurrenceId = missed.GetProperty("occurrence_id").GetString();
        using var attestation = await owner.PostAsJsonAsync(
            $"{controlPath}/occurrences/{occurrenceId}/attestations", new
            {
                expected_revision = 0,
                result = "failed",
                performed_at = DateTimeOffset.UtcNow.AddMinutes(-5),
                notes = "Started the review.",
                rationale = "The access export tool was unavailable.",
                evidence = Array.Empty<object>(),
            });
        var attested = await ReadAsync(attestation);
        using var selfReview = await owner.PostAsJsonAsync(
            $"{controlPath}/occurrences/{occurrenceId}/reviews", new
            {
                expected_revision = 2,
                attestation_id = attested.GetProperty("attestations")[0]
                    .GetProperty("attestation_id").GetString(),
                outcome = "approved",
                rationale = "Self review.",
            });
        var findings = await WaitForAsync(owner, $"{programPath}/findings",
            page => page.GetProperty("items").GetArrayLength() > 0);
        using var work = await owner.GetAsync($"{programPath}/my-work");
        using var outsiderPlan = await outsider.GetAsync($"{controlPath}/operating-plan");
        using var otherProgram = await owner.GetAsync(
            $"/api/v1/tenants/{tenantId}/programs/{Guid.NewGuid()}/controls/{controlId}/operating-plan");

        // Assert
        Assert.Equal(HttpStatusCode.OK, proposal.StatusCode);
        Assert.Equal("pending_approval", proposed.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, selfApproval.StatusCode);
        Assert.Contains(blocked.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("kind").GetString() == "missing_plan");
        Assert.Equal(HttpStatusCode.OK, attestation.StatusCode);
        Assert.Equal("submitted", attested.GetProperty("state").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, selfReview.StatusCode);
        var finding = Assert.Single(findings.GetProperty("items").EnumerateArray());
        Assert.Equal("control_occurrence", finding.GetProperty("source").GetProperty("kind")
            .GetString());
        Assert.Equal("failed: The access export tool was unavailable.",
            finding.GetProperty("source").GetProperty("source_text").GetString());
        Assert.Equal(ownerMemberId.ToString(), finding.GetProperty("owner_member_id").GetString());
        Assert.Equal(HttpStatusCode.OK, work.StatusCode);
        Assert.Equal("owner", (await ReadAsync(work)).GetProperty("responsibilities")[0]
            .GetProperty("role").GetString());
        Assert.True(outsiderPlan.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.NotFound, otherProgram.StatusCode);
    }

    internal static (HttpClient Owner, HttpClient Outsider) CreateClients(
        WebApplicationFactory<Program> factory, bool splitHosts)
    {
        var priorHostMode = TestHostMode.Current;
        try
        {
            TestHostMode.Set(splitHosts ? "api" : "standalone");
            return (factory.CreateClient(), factory.CreateClient());
        }
        finally
        {
            TestHostMode.Set(priorHostMode);
        }
    }

    internal static async Task SeedApprovedControlAsync(WebApplicationFactory<Program> factory,
        Uuid tenantId, Uuid programId, Uuid controlId, Uuid ownerMemberId, DateOnly effectiveFrom)
    {
        var author = Uuid.CreateVersion4();
        var reviewId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        await SeedAsync(factory, new ControlDraft(tenantId, controlId), control =>
        {
            var created = control.Create(programId, Uuid.CreateVersion4(), "AC-E2E",
                new ControlDraftContent("Access review", "Review access",
                    "Management reviews user access", "Monthly review.", ["Signed review"]),
                author, "Author", now);
            if (!created.IsSuccess)
                return Result.Failure(created.Error!);
            var failure = control.AssignResponsibility(new ResponsibilityScope("control",
                    controlId, ControlVersionIds.Initial(controlId), 1), Uuid.CreateVersion4(),
                ownerMemberId, ResponsibilityType.ControlOwner, author, "Author", now,
                now.AddMinutes(-1), null, []) ?? control.Review(programId, 1, reviewId, "accept",
                "Ok", Uuid.CreateVersion4(), "Reviewer", now) ?? control.Approve(programId, 1,
                Uuid.CreateVersion4(), reviewId, effectiveFrom, "Ready",
                new HashSet<Uuid> { ownerMemberId }, Uuid.CreateVersion4(), "Approver", now);
            return Command(failure);
        });
    }

    internal static async Task SeedAsync<TAggregate>(WebApplicationFactory<Program> factory,
        TAggregate aggregate, Func<TAggregate, Result> operation) where TAggregate : Aggregate
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAggregateExecutor>()
            .ExecuteAsync(aggregate, item => AggregateOutcome.Commit(operation(item)),
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    static Result Command(CommandFailure? failure) => failure is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, failure.Message!));

    internal static async Task<(Guid TenantId, Guid ProgramId)> CreateProgramAsync(HttpClient owner)
    {
        using var tenant = await owner.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Control operations tenant",
            slug = $"ops-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, tenant.StatusCode);
        var tenantId = Guid.Parse((await ReadAsync(tenant)).GetProperty("tenant_id").GetString()!);
        await AccessGrantE2ESupport.IssueFounderOrganizationGrantAsync(owner, tenantId);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var program = await owner.PostAsJsonAsync(
                $"/api/v1/tenants/{tenantId}/programs", new
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

    internal static async Task<JsonElement> WaitForAsync(HttpClient client, string path,
        Func<JsonElement, bool> ready)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var body = await ReadAsync(response);
                if (ready(body))
                    return body;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"{path} never reached the expected state.");
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

    internal static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
        .RootElement.Clone();
}
