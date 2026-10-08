using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class NotApplicableOccurrenceReviewTests
{
    [Theory]
    [InlineData("approved")]
    [InlineData("deferred")]
    public async Task ShouldRequireIndependentProjectedReviewGivenNotApplicableAttestation(string outcome)
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        await source.PlanAsync(cadence: new ControlCadence("ad_hoc", DueWithinDays: 10));
        var opened = await source.AsAsync(source.OwnerUserId, new OpenControlOccurrence(source.TenantId,
            source.ProgramId, source.ControlId, "No production changes", source.Today));
        await using var provider = Compose(source);
        var attested = await PersonalOccurrenceProofTransportTests.HttpAsync(provider, source.OwnerUserId,
            source.Attest(opened, "not_applicable", [], "No production changes occurred in this period."));
        await ProjectAsync(provider, source.TenantId);
        var work = await ReviewWorkAsync(provider, source, source.ReviewerUserId);
        var item = Assert.Single(work.Value.Items);
        var before = await ProgramManagementServices.HydrateAsync(provider,
            new ControlOperationsLedger(source.TenantId, source.ProgramId));
        // Give the performer ordinary manager authority so refusal proves source SoD, not a missing reviewer role.
        source.Permissions.Managers.Add(source.OwnerMemberId);

        // Act
        var denied = await PersonalOccurrenceProofTransportTests.HttpAsync(provider, source.OwnerUserId,
            source.Review(attested.Value, outcome), RequestErrorKind.Forbidden);
        var afterDenied = await ProgramManagementServices.HydrateAsync(provider,
            new ControlOperationsLedger(source.TenantId, source.ProgramId));
        var reviewed = await PersonalOccurrenceProofTransportTests.HttpAsync(provider, source.ReviewerUserId,
            source.Review(attested.Value, outcome));
        await ProjectAsync(provider, source.TenantId);
        var after = await ReviewWorkAsync(provider, source, source.ReviewerUserId);
        await using var replayProvider = Compose(source);
        await ProjectAsync(replayProvider, source.TenantId);
        await ProjectAsync(replayProvider, source.TenantId);
        var replayed = await ReviewWorkAsync(replayProvider, source, source.ReviewerUserId);

        // Assert
        Assert.Equal("submitted", attested.Value.State);
        Assert.Equal("not_applicable", attested.Value.Attestations[0].Result);
        Assert.Equal("No production changes occurred in this period.", attested.Value.Attestations[0].Rationale);
        Assert.Empty(attested.Value.Attestations[0].Evidence);
        Assert.Equal(source.OwnerMemberId, Assert.Single(attested.Value.Attestations).RecorderMemberId);
        Assert.Equal("occurrence_review", item.Kind);
        Assert.Equal(opened.OccurrenceId, item.SourceId);
        Assert.Equal(source.ReviewerMemberId, item.Responsible.Id);
        Assert.Equal("review", item.NextAction);
        Assert.Equal("The member who performed or recorded an attestation cannot review it.", denied.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, afterDenied.CommittedStreamPosition);
        var decision = Assert.Single(reviewed.Value.Reviews);
        Assert.Equal(attested.Value.Attestations[0].AttestationId, decision.AttestationId);
        Assert.Equal(1, decision.AttestationVersion);
        Assert.Equal(source.ReviewerMemberId, decision.ReviewerMemberId);
        Assert.Equal(outcome == "deferred" ? 1 : 0, after.Value.Items.Count);
        Assert.Equal(after.Value.Counts, replayed.Value.Counts);
        Assert.Equal(after.Value.Items.Select(value => value.WorkItemId), replayed.Value.Items.Select(value => value.WorkItemId));
        Assert.Equal(after.Value.Items.Count, replayed.Value.Items.Select(value => value.WorkItemId).Distinct().Count());
    }

    [Fact]
    public async Task ShouldRetainExactReviewHistoryGivenReturnedNotApplicableCorrection()
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        await source.PlanAsync(cadence: new ControlCadence("ad_hoc", DueWithinDays: 10));
        var opened = await source.AsAsync(source.OwnerUserId, new OpenControlOccurrence(source.TenantId,
            source.ProgramId, source.ControlId, "Period applicability", source.Today));
        await using var provider = Compose(source);
        var attested = await PersonalOccurrenceProofTransportTests.HttpAsync(provider, source.OwnerUserId,
            source.Attest(opened, "not_applicable", [], "No changes."));
        await ProjectAsync(provider, source.TenantId);
        var initialWork = Assert.Single((await ReviewWorkAsync(provider, source, source.ReviewerUserId)).Value.Items);
        var returned = await PersonalOccurrenceProofTransportTests.HttpAsync(provider, source.ReviewerUserId,
            source.Review(attested.Value, "returned"));
        await ProjectAsync(provider, source.TenantId);
        Assert.Empty((await ReviewWorkAsync(provider, source, source.ReviewerUserId)).Value.Items);
        var corrected = await PersonalOccurrenceProofTransportTests.HttpAsync(provider, source.OwnerUserId,
            new CorrectControlAttestation(source.TenantId, source.ProgramId, source.ControlId, opened.OccurrenceId,
                returned.Value.Revision, "not_applicable", DateTimeOffset.UtcNow.AddMinutes(-1), null, null,
                null, "No production changes; staging-only deployment excluded.", [], "Clarified the population."));
        await ProjectAsync(provider, source.TenantId);
        var correctedWork = Assert.Single((await ReviewWorkAsync(provider, source, source.ReviewerUserId)).Value.Items);
        var before = await ProgramManagementServices.HydrateAsync(provider,
            new ControlOperationsLedger(source.TenantId, source.ProgramId));

        // Act
        var stale = await PersonalOccurrenceProofTransportTests.HttpAsync(provider, source.ReviewerUserId,
            source.Review(corrected.Value, "approved") with { AttestationId = attested.Value.Attestations[0].AttestationId },
            RequestErrorKind.Conflict);
        var afterDenied = await ProgramManagementServices.HydrateAsync(provider,
            new ControlOperationsLedger(source.TenantId, source.ProgramId));
        var approved = await PersonalOccurrenceProofTransportTests.HttpAsync(provider, source.ReviewerUserId,
            source.Review(corrected.Value, "approved"));
        await using var replayProvider = Compose(source);
        await ProjectAsync(replayProvider, source.TenantId);
        var final = await ReviewWorkAsync(replayProvider, source, source.ReviewerUserId);

        // Assert
        Assert.Equal(initialWork.WorkItemId, correctedWork.WorkItemId);
        Assert.Equal("Review the latest attestation version.", stale.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, afterDenied.CommittedStreamPosition);
        Assert.Equal(2, approved.Value.Attestations.Count);
        Assert.Equal(attested.Value.Attestations[0], approved.Value.Attestations[0]);
        Assert.Equal(attested.Value.Attestations[0].AttestationId, approved.Value.Attestations[1].SupersedesAttestationId);
        Assert.Equal(2, approved.Value.Attestations[1].Version);
        Assert.Equal("not_applicable", approved.Value.Attestations[1].Result);
        Assert.Equal(returned.Value.Reviews[0], approved.Value.Reviews[0]);
        Assert.Equal(approved.Value.Attestations[1].AttestationId, approved.Value.Reviews[1].AttestationId);
        Assert.Equal(2, approved.Value.Reviews[1].AttestationVersion);
        Assert.Empty(final.Value.Items);
    }

    static ServiceProvider Compose(OperationsFixture source)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance"
        }).Build(), developerAuthentication: true);
        var events = source.Provider.GetRequiredService<IEventStore>();
        services.AddSingleton(events);
        services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(source.Permissions));
        services.AddSingleton<IProgramResourceScopeResolver, TestProgramResourceScopeResolver>();
        services.AddSingleton<ITenantActivity, ActiveTenant>();
        services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    static async Task<Result<WorkQueueView>> ReviewWorkAsync(IServiceProvider provider, OperationsFixture source, Uuid actor)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new ListWork(source.TenantId, source.ProgramId, "mine", Search: "occurrence_review"),
            new RequestDispatchContext(ProgramManagementServices.Actor(actor), new DirectInvocation()), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    static async Task ProjectAsync(IServiceProvider provider, Uuid tenantId)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        foreach (var directory in services.GetServices<IAccountableWorkItemDirectoryReader>()
                     .DistinctBy(reader => reader.ProjectorName))
        {
            var registration = Assert.Single(services.GetServices<WorkloadRegistration>(), value => value.Name == directory.ProjectorName);
            var projector = (Projector)services.GetRequiredService(registration.ComponentType);
            await new ProjectorScenario(new TenantId(tenantId.ToString())).RunAsync(projector);
            var checkpoint = await directory.LoadCheckpointAsync(tenantId);
            var runner = new ProjectorRunner(services.GetRequiredService<IDomainEventReader>());
            while (true)
            {
                var next = await runner.RunAsync(projector, checkpoint);
                if (next == checkpoint)
                    break;
                checkpoint = next;
            }
        }
    }
}
