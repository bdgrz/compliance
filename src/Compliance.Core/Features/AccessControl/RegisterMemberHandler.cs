using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RegisterMemberHandler(IAggregateRepository repository) : IRequestHandler<RegisterMember>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RegisterMember> context, CancellationToken ct)
    {
        var member = await repository.HydrateAsync(
            new Member(context.Request.TenantId, context.Request.UserId), ct);
        var version = member.Version;
        var result = member.Register();
        if (result.IsSuccess && member.Version != version)
            await repository.SaveAsync(member, context, ct);
        return result;
    }
}
