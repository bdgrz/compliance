using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Turns an open policy or training campaign's unsatisfied people into work (M0-D15, M0-D12). A person with a
///     platform membership gets their own item; for anyone else the campaign owner collects and records the
///     acknowledgement or completion.
/// </summary>
static class PolicyCampaignWork
{
    public const string Acknowledgement = "policy_acknowledgement";
    public const string TrainingCompletion = "training_completion";

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
                    if (record.IsCreated && record.CorrelatedUserId is { } userId)
                        members[person.PersonId] = RbacIds.Member(tenantId, userId);
                }
                candidates.AddRange(Candidates(campaign, today, members));
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
        IReadOnlyDictionary<Uuid, Uuid> memberByPerson)
    {
        if (!campaign.IsLaunched || campaign.IsClosed || campaign.Subject is not { } subject)
            return [];
        var training = subject.Kind == "training";
        var kind = training ? TrainingCompletion : Acknowledgement;
        var path = $"/api/v1/tenants/{campaign.TenantId}/programs/{campaign.ProgramId}/campaigns/{campaign.Id}/" +
                   (training ? "completions" : "acknowledgements");
        var label = $"{subject.Identifier} v{subject.Version} {subject.Title}";
        var owner = new OperatingHolder(OperatingAuthority.MemberHolder, campaign.OwnerMemberId);
        var candidates = new List<WorkCandidate>();
        foreach (var person in campaign.Participants(today, null)
                     .Where(static person => person.State is "pending" or "overdue"))
        {
            var sourceId = Uuid.CreateVersion5(campaign.Id, "participant:" + person.PersonId);
            var member = memberByPerson.TryGetValue(person.PersonId, out var memberId) ? memberId : (Uuid?)null;
            candidates.Add(member is { } own
                ? new WorkCandidate(WorkCandidate.IdFor(sourceId, kind), kind, sourceId, null, null,
                    training ? $"Complete {label}" : $"Acknowledge {label}",
                    training
                        ? "You are in this training campaign's audience."
                        : "You are in this policy campaign's audience.",
                    person.DueOn, null, training ? "complete" : "acknowledge", path,
                    new OperatingHolder(OperatingAuthority.MemberHolder, own), null, new HashSet<Uuid>(),
                    campaign.LaunchedAt)
                : new WorkCandidate(WorkCandidate.IdFor(sourceId, kind), kind, sourceId, null, null,
                    training
                        ? $"Collect {label} completion from {person.DisplayName}"
                        : $"Collect {label} acknowledgement from {person.DisplayName}",
                    $"{person.DisplayName} has no platform membership, so the campaign owner records the result.",
                    person.DueOn, null, training ? "record_completion" : "record_acknowledgement", path, owner,
                    null, new HashSet<Uuid>(), campaign.LaunchedAt));
        }
        return candidates;
    }
}
