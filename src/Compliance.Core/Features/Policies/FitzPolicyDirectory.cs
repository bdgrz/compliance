using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

sealed class FitzPolicyDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/policy-directory/projection", ProjectorName),
        IPolicyDirectoryReader, IPolicyDirectoryProjection
{
    public const string ProjectorName = "PolicyDirectoryV1";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "policies")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is PolicyDraftCreated created)
        {
            await PolicyDirectorySchema.Policies.InsertAsync(Transaction, new PolicyDirectoryRow(
                new PolicySummaryView(created.TenantId, created.ProgramId, created.PolicyId,
                    created.Identifier, created.Content.Title, "draft", "draft", 1, null, null,
                    null, false, created.ChangedAt),
                created.Content.Title, created.Content.ReviewCadenceMonths, null, null, false),
                ct).ConfigureAwait(false);
            return;
        }
        var policyId = domainEvent switch
        {
            PolicyDraftRevised ev => ev.PolicyId,
            PolicyReviewed ev => ev.PolicyId,
            PolicyApproved ev => ev.PolicyId,
            PolicyPeriodicReviewConfirmed ev => ev.PolicyId,
            PolicyRetirementProposed ev => ev.PolicyId,
            PolicyRetired ev => ev.PolicyId,
            PolicyDraftDiscarded ev => ev.PolicyId,
            _ => (Uuid?)null,
        };
        if (policyId is not { } id)
            return;
        var row = await PolicyDirectorySchema.Policies.GetAsync(Transaction, id, ct)
            .ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A policy event cannot project before its draft was created.");
        if (domainEvent is PolicyDraftDiscarded)
        {
            await PolicyDirectorySchema.Policies.DeleteAsync(Transaction, id, ct)
                .ConfigureAwait(false);
            return;
        }
        await PolicyDirectorySchema.Policies.ReplaceAsync(Transaction, row, Apply(row,
            domainEvent), ct).ConfigureAwait(false);
    }

    static PolicyDirectoryRow Apply(PolicyDirectoryRow row, DomainEvent domainEvent)
    {
        var summary = row.Summary;
        switch (domainEvent)
        {
            case PolicyDraftRevised revised:
                return row with
                {
                    Summary = summary with
                    {
                        Revision = revised.Revision,
                        PendingStatus = "draft",
                        Title = summary.CurrentVersion is null ? revised.Content.Title : summary.Title,
                        LastChangedAt = revised.ChangedAt,
                    },
                    DraftTitle = revised.Content.Title,
                    DraftReviewCadenceMonths = revised.Content.ReviewCadenceMonths,
                };
            case PolicyReviewed reviewed:
                var accepted = reviewed.Outcome == "accept";
                return row with
                {
                    Summary = summary with
                    {
                        PendingStatus = row.RetirementPending
                            ? accepted ? "retirement_awaiting_approval" : null
                            : accepted ? "awaiting_approval" : "changes_requested",
                        LastChangedAt = reviewed.DecidedAt,
                    },
                    RetirementPending = row.RetirementPending && accepted,
                };
            case PolicyApproved approved:
                var reviewedOn = DateOnly.FromDateTime(approved.DecidedAt.UtcDateTime);
                return row with
                {
                    Summary = summary with
                    {
                        Status = "approved",
                        PendingStatus = null,
                        Title = row.DraftTitle,
                        CurrentVersion = approved.Version,
                        CurrentEffectiveFrom = approved.EffectiveFrom,
                        NextReviewDueOn = reviewedOn.AddMonths(row.DraftReviewCadenceMonths),
                        LastChangedAt = approved.DecidedAt,
                    },
                    CurrentReviewCadenceMonths = row.DraftReviewCadenceMonths,
                    LastReviewedOn = reviewedOn,
                };
            case PolicyPeriodicReviewConfirmed confirmed:
                return row with
                {
                    Summary = summary with
                    {
                        NextReviewDueOn = confirmed.ReviewedOn.AddMonths(
                            row.CurrentReviewCadenceMonths ?? row.DraftReviewCadenceMonths),
                        LastChangedAt = confirmed.DecidedAt,
                    },
                    LastReviewedOn = confirmed.ReviewedOn,
                };
            case PolicyRetirementProposed proposed:
                return row with
                {
                    Summary = summary with
                    {
                        Revision = proposed.Revision,
                        PendingStatus = "retirement_proposed",
                        LastChangedAt = proposed.ProposedAt,
                    },
                    RetirementPending = true,
                };
            case PolicyRetired retired:
                return row with
                {
                    Summary = summary with
                    {
                        Status = "retired",
                        PendingStatus = null,
                        NextReviewDueOn = null,
                        LastChangedAt = retired.DecidedAt,
                    },
                    RetirementPending = false,
                };
            default:
                return row;
        }
    }

    public async ValueTask<Page<PolicySummaryView>> ListProgramAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var page = await PolicyDirectorySchema.Policies.QueryAsync(tx,
            PolicyDirectorySchema.ByProgramIdentifier.Query()
                .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
        return new Page<PolicySummaryView>(
            page.Items.Select(static row => row.Summary).ToArray(), page.NextCursor);
    }
}
