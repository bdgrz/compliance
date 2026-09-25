using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests;

sealed class UnprotectedCompositionRequestHandler : IRequestHandler<UnprotectedCompositionRequest>
{
    public ValueTask<Result> HandleAsync(
        IRequestContext<UnprotectedCompositionRequest> context,
        CancellationToken ct) => ValueTask.FromResult(Result.Success);
}
