using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests;

sealed class RequestContext<TRequest>(TRequest request, ClaimsPrincipal actor) : IRequestContext<TRequest>
{
    public TRequest Request { get; } = request;
    public ClaimsPrincipal Actor => actor;
    public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
    public Uuid RequestId { get; } = Uuid.CreateVersion4();
    public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
    public Uuid? CausationId => null;
    public Uuid CauseId => RequestId;
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
    public RequestInvocation Invocation { get; } = new DirectInvocation();
}
