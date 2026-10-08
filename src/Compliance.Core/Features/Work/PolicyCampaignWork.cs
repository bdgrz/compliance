using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Turns an open policy or training campaign's unsatisfied people into work (M0-D15, M0-D12). A person with a
///     platform membership acknowledges personally. Campaign owners with current program-management authority
///     record training evidence and acknowledgements for people without platform membership.
/// </summary>
static class PolicyCampaignWork
{
    public const string Acknowledgement = "policy_acknowledgement";
    public const string TrainingCompletion = "training_completion";

    public static IReadOnlyCollection<string> ProjectedKinds => [Acknowledgement, TrainingCompletion];

    public static bool RequiresProgramManagement(WorkCandidate candidate) =>
        candidate.Kind == TrainingCompletion ||
        candidate.Kind == Acknowledgement && candidate.NextAction == "record_acknowledgement";

    public static bool IsFullyProjected(IReadOnlySet<string> projectedKinds) =>
        ProjectedKinds.All(projectedKinds.Contains);

    /// <summary>
    ///     Loads work for the program's open campaigns. The directory only enumerates campaign identities; each
    ///     campaign and each person's membership link are read from their source ledgers.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadAsync(
        IAggregateReader reader, ICampaignDirectoryReader directory,
        CampaignDirectoryReadConsistency consistency, Uuid tenantId, Uuid programId,
        DateOnly today, CancellationToken ct)
    {
        var fence = await consistency.CaptureAsync(tenantId, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<IReadOnlyList<WorkCandidate>>.Failure(fence.Error);

        var candidates = new List<WorkCandidate>();
        var members = new Dictionary<Uuid, Uuid>();
        var people = new Dictionary<Uuid, PolicyAcknowledgementPersonSnapshot>();
        var resolved = new HashSet<Uuid>();
        string? cursor = null;
        do
        {
            var page = await directory.ListProgramAsync(tenantId, programId, 200, cursor, ct).ConfigureAwait(false);
            foreach (var summary in page.Items.Where(summary => summary.TenantId == tenantId &&
                                                               summary.ProgramId == programId &&
                                                               summary.Status == "open"))
            {
                var campaign = await reader.HydrateAsync(new PolicyDistributionCampaign(tenantId,
                    summary.CampaignId), ct).ConfigureAwait(false);
                if (!campaign.IsLaunched || campaign.ProgramId != programId)
                    continue;
                foreach (var person in campaign.Participants(today, null)
                             .Where(person => person.State is "pending" or "overdue" && resolved.Add(person.PersonId)))
                {
                    var record = await reader.HydrateAsync(new Person(tenantId, person.PersonId), ct)
                        .ConfigureAwait(false);
                    people[person.PersonId] = PolicyAcknowledgementPersonSnapshot.Capture(record);
                    if (record.IsCreated && record.CorrelatedUserId is { } userId)
                        members[person.PersonId] = RbacIds.Member(tenantId, userId);
                }
                candidates.AddRange(Candidates(campaign, today, members, people));
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(tenantId,
            fence.Value, ct).ConfigureAwait(false);
        return confirmed.IsSuccess
            ? Result<IReadOnlyList<WorkCandidate>>.Success(candidates)
            : Result<IReadOnlyList<WorkCandidate>>.Failure(confirmed.Error);
    }

    public static IReadOnlyList<WorkCandidate> Candidates(PolicyDistributionCampaign campaign, DateOnly today,
        IReadOnlyDictionary<Uuid, Uuid> memberByPerson,
        IReadOnlyDictionary<Uuid, PolicyAcknowledgementPersonSnapshot>? people = null)
    {
        if (!campaign.IsLaunched || campaign.IsClosed || campaign.Subject is not { } subject)
            return [];
        var candidates = new List<WorkCandidate>();
        foreach (var person in campaign.Participants(today, null)
                     .Where(static person => person.State is "pending" or "overdue"))
        {
            var member = memberByPerson.TryGetValue(person.PersonId, out var memberId) ? memberId : (Uuid?)null;
            candidates.Add(CreateCandidate(campaign.TenantId, campaign.ProgramId, campaign.Id,
                subject, campaign.OwnerMemberId, campaign.LaunchedAt, person.PersonId,
                person.DisplayName, person.DueOn, member) with
            {
                AcknowledgementPerson = people is not null && people.TryGetValue(person.PersonId, out var captured)
                    ? captured : null,
            });
        }
        return candidates;
    }

    public static WorkCandidate CreateCandidate(Uuid tenantId, Uuid programId, Uuid campaignId,
        CampaignSubject subject, Uuid ownerMemberId, DateTimeOffset launchedAt, Uuid personId,
        string displayName, DateOnly dueOn, Uuid? memberId)
    {
        var training = subject.Kind == "training";
        var kind = training ? TrainingCompletion : Acknowledgement;
        var sourceId = Uuid.CreateVersion5(campaignId, "participant:" + personId);
        var path = $"/api/v1/tenants/{tenantId}/programs/{programId}/campaigns/{campaignId}/" +
                   (training ? "completions" : "acknowledgements");
        var label = $"{subject.Identifier} v{subject.Version} {subject.Title}";
        var owner = new OperatingHolder(OperatingAuthority.MemberHolder, ownerMemberId);
        if (training)
            return new WorkCandidate(WorkCandidate.IdFor(sourceId, kind), kind, sourceId, null, null,
                $"Record {label} completion for {displayName}",
                $"{displayName}'s training completion requires reviewed manual or LMS-export evidence.",
                dueOn, null, "record_completion", path, owner,
                new OperatingHolder(OperatingAuthority.ProgramRecorderHolder, programId),
                new HashSet<Uuid>(), launchedAt);
        return memberId is { } member
            ? new WorkCandidate(WorkCandidate.IdFor(sourceId, kind), kind, sourceId, null, null,
                $"Acknowledge {label}", "You are in this policy campaign's audience.",
                dueOn, null, "acknowledge", path,
                new OperatingHolder(OperatingAuthority.MemberHolder, member), null,
                new HashSet<Uuid>(), launchedAt)
            : new WorkCandidate(WorkCandidate.IdFor(sourceId, kind), kind, sourceId, null, null,
                $"Collect {label} acknowledgement from {displayName}",
                $"{displayName} has no platform membership, so the campaign owner records the result.",
                dueOn, null, "record_acknowledgement", path, owner,
                new OperatingHolder(OperatingAuthority.ProgramRecorderHolder, programId),
                new HashSet<Uuid>(), launchedAt);
    }
}
