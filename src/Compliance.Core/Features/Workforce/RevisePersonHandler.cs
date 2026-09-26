using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class RevisePersonHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RevisePerson>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RevisePerson> context,
        CancellationToken ct)
    {
        var actor = WorkforceActor.From(context);
        var request = context.Request;
        return executor.ExecuteAsync(new Person(request.TenantId, request.PersonId),
            person => AggregateOutcome.CommitOnSuccess(person.Revise(request.ExpectedRevision,
                request.DisplayName, request.WorkEmail, actor, clock.GetUtcNow())),
            context, ct);
    }
}
