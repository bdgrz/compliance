using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class RecordPersonHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<RecordPerson, PersonRegistration>
{
    public ValueTask<Result<PersonRegistration>> HandleAsync(
        IRequestContext<RecordPerson> context, CancellationToken ct)
    {
        var actor = WorkforceActor.From(context);
        var request = context.Request;
        return executor.ExecuteAsync(new Person(request.TenantId, context.RequestId),
            person => AggregateOutcome.CommitOnSuccess(person.Record(request.DisplayName,
                request.WorkEmail, actor, clock.GetUtcNow())), context, ct);
    }
}
