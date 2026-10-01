using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     Lists a program's policies from the projection after proving it reached the source, and
///     flags any whose periodic review is overdue today.
/// </summary>
public sealed class ListPoliciesHandler(IPolicyDirectoryReader directory,
    IDomainEventReader events, TimeProvider clock)
    : IRequestHandler<ListPolicies, Page<PolicySummaryView>>
{
    public async ValueTask<Result<Page<PolicySummaryView>>> HandleAsync(
        IRequestContext<ListPolicies> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<PolicySummaryView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "Limit must be between 1 and 200."));
        var checkpoint = await directory.LoadCheckpointAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        await using (var pending = events.ReadAsync(EventStreamPattern.ForPattern(
                         request.TenantId.ToString(), "policies"), checkpoint.Cursor, ct)
                     .GetAsyncEnumerator(ct))
        {
            if (await pending.MoveNextAsync().ConfigureAwait(false))
                return Result<Page<PolicySummaryView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict, "The policy list projection has not reached the source.",
                    isTransient: true));
        }
        Page<PolicySummaryView> page;
        try
        {
            page = await directory.ListProgramAsync(request.TenantId, request.ProgramId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<PolicySummaryView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The policy cursor is invalid."));
        }
        if (page.Items.Any(item => item.TenantId != request.TenantId ||
                                   item.ProgramId != request.ProgramId))
            return Result<Page<PolicySummaryView>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The policy projection has an invalid program scope."));
        var today = PolicySource.Today(clock);
        return Result<Page<PolicySummaryView>>.Success(new Page<PolicySummaryView>(
            page.Items.Select(item => item with
            {
                ReviewOverdue = item.NextReviewDueOn is { } due && today > due,
            }).ToArray(), page.NextCursor));
    }
}
