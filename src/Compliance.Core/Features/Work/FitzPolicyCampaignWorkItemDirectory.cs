using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzPolicyCampaignWorkItemDirectory(IKvClient client, IAggregateReader sourceReader)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/policy-campaigns-v1", ProjectorName),
      IAccountableWorkItemDirectoryReader, IPolicyCampaignWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemPolicyCampaignV1";
    const string RevisionKey = ProjectorName;

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds => PolicyCampaignWork.ProjectedKinds;

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "policy-distribution-campaigns");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await PolicyCampaignWorkItemDirectorySchema.Revisions.GetAsync(tx,
            RevisionKey, ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case PolicyCampaignLaunched launched:
                await ApplyLaunchAsync(launched, ct).ConfigureAwait(false);
                break;
            case PolicyCampaignAudienceFrozen frozen:
                await ApplyAudienceAsync(frozen, ct).ConfigureAwait(false);
                break;
            case PolicyCampaignAudienceAmended amended:
                await ApplyAmendmentsAsync(amended, ct).ConfigureAwait(false);
                break;
            case PolicyAcknowledgementRecorded acknowledged:
                await ApplyAcknowledgementAsync(acknowledged, ct).ConfigureAwait(false);
                break;
            case TrainingCompletionRecorded completed:
                await ApplyCompletionAsync(completed, ct).ConfigureAwait(false);
                break;
            case CampaignWaiverApproved waiver:
                await ApplyWaiverAsync(waiver, ct).ConfigureAwait(false);
                break;
            case PolicyCampaignClosed closed:
                await ApplyClosedAsync(closed, ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    $"The policy campaign work projection cannot apply {domainEvent.GetType().Name}.");
        }

        await IncrementRevisionAsync(ct).ConfigureAwait(false);
    }

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default) =>
        LoadProgramAsync(tenantId, programId, DateTimeOffset.UtcNow, DateOnly.MaxValue,
            null, ct);

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, DateOnly today, DateOnly horizon, Uuid? workItemId,
        CancellationToken ct = default) => LoadProgramAsync(tenantId, programId,
        new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), horizon,
        workItemId, ct);

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, DateTimeOffset now, DateOnly horizon, Uuid? workItemId,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var candidates = new List<WorkCandidate>();
        var members = new Dictionary<Uuid, Uuid>();
        var people = new Dictionary<Uuid, PolicyAcknowledgementPersonSnapshot>();
        var resolvedPeople = new HashSet<Uuid>();
        string? campaignCursor = null;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        do
        {
            var campaignPage = await PolicyCampaignWorkItemDirectorySchema.Campaigns.QueryAsync(tx,
                PolicyCampaignWorkItemDirectorySchema.CampaignsByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(campaignCursor), ct)
                .ConfigureAwait(false);
            foreach (var campaign in campaignPage.Items)
            {
                if (!IsValidCampaign(campaign, tenantId, programId))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
                if (campaign.IsClosed)
                    continue;

                string? participantCursor = null;
                do
                {
                    var participantPage = await PolicyCampaignWorkItemDirectorySchema.Participants
                        .QueryAsync(tx, PolicyCampaignWorkItemDirectorySchema.ParticipantsByCampaign
                            .Query().WithPrefix(programId.ToString(), campaign.CampaignId.ToString())
                            .Take(200).After(participantCursor), ct).ConfigureAwait(false);
                    foreach (var participant in participantPage.Items)
                    {
                        if (!IsValidParticipant(participant, tenantId, programId, campaign.CampaignId))
                            return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
                        if (!IsActionable(participant, today) || participant.DueOn > horizon)
                            continue;

                        var kind = campaign.Subject.Kind == "training"
                            ? PolicyCampaignWork.TrainingCompletion
                            : PolicyCampaignWork.Acknowledgement;
                        var sourceId = Uuid.CreateVersion5(campaign.CampaignId,
                            "participant:" + participant.PersonId);
                        var candidateId = WorkCandidate.IdFor(sourceId, kind);
                        if (workItemId is { } wanted && candidateId != wanted)
                            continue;

                        if (resolvedPeople.Add(participant.PersonId))
                        {
                            var person = await sourceReader.HydrateAsync(new Person(tenantId,
                                participant.PersonId), ct).ConfigureAwait(false);
                            people[participant.PersonId] = PolicyAcknowledgementPersonSnapshot.Capture(person);
                            if (person.IsCreated && person.CorrelatedUserId is { } userId)
                                members[participant.PersonId] = RbacIds.Member(tenantId, userId);
                        }

                        var memberId = members.TryGetValue(participant.PersonId, out var mapped)
                            ? mapped
                            : (Uuid?)null;
                        candidates.Add(PolicyCampaignWork.CreateCandidate(tenantId, programId,
                            campaign.CampaignId, campaign.Subject, campaign.OwnerMemberId,
                            campaign.LaunchedAt, participant.PersonId, participant.DisplayName,
                            participant.DueOn, memberId) with
                        {
                            AcknowledgementPerson = people[participant.PersonId],
                        });
                    }
                    participantCursor = participantPage.NextCursor;
                } while (participantCursor is not null);
            }
            campaignCursor = campaignPage.NextCursor;
        } while (campaignCursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    async ValueTask ApplyLaunchAsync(PolicyCampaignLaunched ev, CancellationToken ct)
    {
        if (ev.TenantId == Uuid.Empty || ev.ProgramId == Uuid.Empty || ev.CampaignId == Uuid.Empty ||
            ev.ActorMemberId == Uuid.Empty)
            throw new InvalidOperationException("A campaign launch has an invalid scope.");
        if (ev.Subject.Kind is not ("policy" or "training") || ev.Subject.Version < 1 ||
            ev.AudienceCount < 0)
            throw new InvalidOperationException("A campaign launch has invalid work terms.");
        if (await PolicyCampaignWorkItemDirectorySchema.Campaigns.GetAsync(Transaction,
                ev.CampaignId, ct).ConfigureAwait(false) is not null)
            throw new InvalidOperationException("A campaign launch cannot replace projected work state.");

        await PolicyCampaignWorkItemDirectorySchema.Campaigns.InsertAsync(Transaction,
            new PolicyCampaignWorkCampaignState(ev.TenantId, ev.ProgramId, ev.CampaignId,
                ev.Subject, ev.ActorMemberId, ev.LaunchedAt, false), ct).ConfigureAwait(false);
    }

    async ValueTask ApplyAudienceAsync(PolicyCampaignAudienceFrozen ev, CancellationToken ct)
    {
        var campaign = await RequireCampaignAsync(ev.TenantId, ev.CampaignId, ct)
            .ConfigureAwait(false);
        foreach (var entry in ev.Entries)
        {
            var participant = new PolicyCampaignWorkParticipantState(campaign.TenantId,
                campaign.ProgramId, campaign.CampaignId, entry.PersonId, entry.DisplayName,
                entry.DueOn, true);
            if (await PolicyCampaignWorkItemDirectorySchema.Participants.GetAsync(Transaction,
                    PolicyCampaignWorkItemDirectorySchema.Key(ev.CampaignId, entry.PersonId), ct)
                    .ConfigureAwait(false) is not null)
                throw new InvalidOperationException("A campaign participant cannot be launched twice.");
            await PolicyCampaignWorkItemDirectorySchema.Participants.InsertAsync(Transaction,
                participant, ct).ConfigureAwait(false);
        }
    }

    async ValueTask ApplyAmendmentsAsync(PolicyCampaignAudienceAmended ev, CancellationToken ct)
    {
        var campaign = await RequireCampaignAsync(ev.TenantId, ev.CampaignId, ct)
            .ConfigureAwait(false);
        foreach (var amendment in ev.Amendments)
        {
            var key = PolicyCampaignWorkItemDirectorySchema.Key(ev.CampaignId, amendment.PersonId);
            var current = await PolicyCampaignWorkItemDirectorySchema.Participants.GetAsync(
                Transaction, key, ct).ConfigureAwait(false);
            if (amendment.Reason is "joiner" or "mover_in")
            {
                if (amendment.DueOn is not { } dueOn ||
                    string.IsNullOrWhiteSpace(amendment.DisplayName))
                    throw new InvalidOperationException("A campaign audience addition is incomplete.");
                var next = current is null
                    ? new PolicyCampaignWorkParticipantState(campaign.TenantId, campaign.ProgramId,
                        campaign.CampaignId, amendment.PersonId, amendment.DisplayName, dueOn, true)
                    : current with
                    {
                        DisplayName = amendment.DisplayName,
                        DueOn = dueOn,
                        IsActive = true,
                    };
                if (current is null)
                    await PolicyCampaignWorkItemDirectorySchema.Participants.InsertAsync(Transaction,
                        next, ct).ConfigureAwait(false);
                else
                    await PolicyCampaignWorkItemDirectorySchema.Participants.ReplaceAsync(Transaction,
                        current, next, ct).ConfigureAwait(false);
            }
            else if (amendment.Reason is "mover_out" or "leaver")
            {
                if (current is null || !current.IsActive)
                    throw new InvalidOperationException("A campaign audience removal has no active participant.");
                await PolicyCampaignWorkItemDirectorySchema.Participants.ReplaceAsync(Transaction,
                    current, current with { IsActive = false }, ct).ConfigureAwait(false);
            }
            else
                throw new InvalidOperationException("A campaign audience amendment has an unknown reason.");
        }
    }

    async ValueTask ApplyAcknowledgementAsync(PolicyAcknowledgementRecorded ev,
        CancellationToken ct)
    {
        var campaign = await RequireCampaignAsync(ev.TenantId, ev.CampaignId, ct)
            .ConfigureAwait(false);
        if (campaign.Subject.Kind != "policy")
            throw new InvalidOperationException("A training campaign cannot project an acknowledgement.");
        await UpdateParticipantAsync(ev.TenantId, campaign.ProgramId, ev.CampaignId, ev.PersonId,
            participant => participant with { AcknowledgedAt = ev.AcknowledgedAt }, ct)
            .ConfigureAwait(false);
    }

    async ValueTask ApplyCompletionAsync(TrainingCompletionRecorded ev, CancellationToken ct)
    {
        var campaign = await RequireCampaignAsync(ev.TenantId, ev.CampaignId, ct)
            .ConfigureAwait(false);
        if (campaign.Subject.Kind != "training")
            throw new InvalidOperationException("A policy campaign cannot project a training completion.");
        await UpdateParticipantAsync(ev.TenantId, campaign.ProgramId, ev.CampaignId, ev.PersonId,
            participant => participant with
            {
                CompletedOn = ev.CompletedOn,
                CompletionRecordedAt = ev.RecordedAt,
            }, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyWaiverAsync(CampaignWaiverApproved ev, CancellationToken ct)
    {
        var campaign = await RequireCampaignAsync(ev.TenantId, ev.CampaignId, ct)
            .ConfigureAwait(false);
        await UpdateParticipantAsync(ev.TenantId, campaign.ProgramId, ev.CampaignId, ev.PersonId,
            participant => participant with
            {
                WaiverApprovedAt = ev.ApprovedAt,
                WaiverExpiresOn = ev.ExpiresOn,
            }, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyClosedAsync(PolicyCampaignClosed ev, CancellationToken ct)
    {
        var current = await RequireCampaignAsync(ev.TenantId, ev.CampaignId, ct)
            .ConfigureAwait(false);
        await PolicyCampaignWorkItemDirectorySchema.Campaigns.ReplaceAsync(Transaction, current,
            current with { IsClosed = true }, ct).ConfigureAwait(false);
    }

    async ValueTask UpdateParticipantAsync(Uuid tenantId, Uuid programId, Uuid campaignId,
        Uuid personId, Func<PolicyCampaignWorkParticipantState, PolicyCampaignWorkParticipantState> update,
        CancellationToken ct)
    {
        var key = PolicyCampaignWorkItemDirectorySchema.Key(campaignId, personId);
        var current = await PolicyCampaignWorkItemDirectorySchema.Participants.GetAsync(Transaction,
            key, ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A campaign result cannot precede its audience participant.");
        if (current.TenantId != tenantId || current.ProgramId != programId ||
            current.CampaignId != campaignId || current.PersonId != personId)
            throw new InvalidOperationException("A campaign result cannot change participant scope.");
        await PolicyCampaignWorkItemDirectorySchema.Participants.ReplaceAsync(Transaction, current,
            update(current), ct).ConfigureAwait(false);
    }

    async ValueTask<PolicyCampaignWorkCampaignState> RequireCampaignAsync(Uuid tenantId,
        Uuid campaignId, CancellationToken ct)
    {
        var campaign = await PolicyCampaignWorkItemDirectorySchema.Campaigns.GetAsync(Transaction,
            campaignId, ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "Campaign work cannot be projected before its launch.");
        if (campaign.TenantId != tenantId || campaign.CampaignId != campaignId)
            throw new InvalidOperationException("A campaign work event cannot change campaign scope.");
        return campaign;
    }

    async ValueTask IncrementRevisionAsync(CancellationToken ct)
    {
        var current = await PolicyCampaignWorkItemDirectorySchema.Revisions.GetAsync(Transaction,
            RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await PolicyCampaignWorkItemDirectorySchema.Revisions.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await PolicyCampaignWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction, current,
                next, ct).ConfigureAwait(false);
    }

    static bool IsValidCampaign(PolicyCampaignWorkCampaignState campaign, Uuid tenantId,
        Uuid programId) => campaign.TenantId == tenantId && campaign.ProgramId == programId &&
        campaign.CampaignId != Uuid.Empty && campaign.Subject is not null &&
        campaign.Subject.Kind is "policy" or "training" && campaign.Subject.Version > 0 &&
        campaign.OwnerMemberId != Uuid.Empty && campaign.LaunchedAt != DateTimeOffset.MinValue;

    static bool IsValidParticipant(PolicyCampaignWorkParticipantState participant, Uuid tenantId,
        Uuid programId, Uuid campaignId) => participant.TenantId == tenantId &&
        participant.ProgramId == programId && participant.CampaignId == campaignId &&
        participant.PersonId != Uuid.Empty && !string.IsNullOrWhiteSpace(participant.DisplayName);

    static bool IsActionable(PolicyCampaignWorkParticipantState participant, DateOnly asOf)
    {
        if (!participant.IsActive)
            return false;
        if (participant.AcknowledgedAt is { } acknowledgedAt &&
            DateOnly.FromDateTime(acknowledgedAt.UtcDateTime) <= asOf)
            return false;
        if (participant.CompletedOn is { } completedOn && completedOn <= asOf &&
            participant.CompletionRecordedAt is { } recordedAt &&
            DateOnly.FromDateTime(recordedAt.UtcDateTime) <= asOf)
            return false;
        if (participant.WaiverExpiresOn is { } expiresOn &&
            participant.WaiverApprovedAt is { } approvedAt &&
            DateOnly.FromDateTime(approvedAt.UtcDateTime) <= asOf && asOf < expiresOn)
            return false;
        return true;
    }

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The policy campaign work projection has an invalid tenant or program scope.");
}
